#!/usr/bin/env bash
# FloV:MP — установка сервера лицензий (flovmp-license) на VDS платформы.
# Один раз; повторный запуск обновляет программу, базу, ключи и релизы не трогает.
#
#   sudo bash setup-vds.sh <папка>
#
# В папке: flovmp-license/ (dotnet publish FloVMP.LicenseAuthority),
# authority.pem (закрытый ключ подписи лицензий — только при первой установке),
# get.sh, get.ps1 (загрузчики для покупателей).
#
# Ставит: .NET 8 Runtime (если нет), базу flovmp_licensing и пользователя в
# MariaDB (случайный пароль), /etc/flovmp-license/license.env (600),
# /opt/flovmp-license, службу flovmp-license (127.0.0.1:7800, пользователь
# flovmp-license), в nginx — внутреннюю папку для отдачи пакетов.
set -euo pipefail
SRC="$(cd "${1:-.}" && pwd)"
ETC=/etc/flovmp-license
DATA=/var/lib/flovmp-license
APP=/opt/flovmp-license
SITE=/etc/nginx/sites-enabled/default
DB=flovmp_licensing
DBUSER=flovmp_license

[ "$(id -u)" -eq 0 ] || { echo "запустите через sudo"; exit 1; }
[ -f "$SRC/flovmp-license/flovmp-license.dll" ] || { echo "нет $SRC/flovmp-license/flovmp-license.dll"; exit 1; }

echo "==> .NET 8"
DOTNET="$(command -v dotnet || true)"
if [ -z "$DOTNET" ] || ! "$DOTNET" --list-runtimes 2>/dev/null | grep -q "Microsoft.NETCore.App 8\."; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet
  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
  DOTNET=/usr/bin/dotnet
fi
"$DOTNET" --list-runtimes | grep "NETCore.App 8" | head -1

echo "==> пользователь и папки"
id flovmp-license >/dev/null 2>&1 || useradd --system --home-dir "$DATA" --shell /usr/sbin/nologin flovmp-license
install -d -m 0750 -o root -g flovmp-license "$ETC"
install -d -m 0755 -o flovmp-license -g flovmp-license "$DATA" "$DATA/releases"
rm -rf "$APP.new" && cp -r "$SRC/flovmp-license" "$APP.new" && rm -rf "$APP" && mv "$APP.new" "$APP"
chmod -R a+rX "$APP"

echo "==> ключ подписи"
if [ ! -f "$ETC/authority.pem" ]; then
  [ -f "$SRC/authority.pem" ] || { echo "нет $SRC/authority.pem (закрытый ключ, открытая часть которого вшита в сервер FloV:MP)"; exit 1; }
  install -m 0640 -o root -g flovmp-license "$SRC/authority.pem" "$ETC/authority.pem"
  shred -u "$SRC/authority.pem" 2>/dev/null || rm -f "$SRC/authority.pem"
fi

echo "==> база MariaDB"
if [ ! -f "$ETC/license.env" ]; then
  PASS="$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 32)"
  mysql -uroot <<SQL
CREATE DATABASE IF NOT EXISTS $DB CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$DBUSER'@'127.0.0.1' IDENTIFIED BY '$PASS';
CREATE USER IF NOT EXISTS '$DBUSER'@'localhost' IDENTIFIED BY '$PASS';
ALTER USER '$DBUSER'@'127.0.0.1' IDENTIFIED BY '$PASS';
ALTER USER '$DBUSER'@'localhost' IDENTIFIED BY '$PASS';
GRANT ALL PRIVILEGES ON $DB.* TO '$DBUSER'@'127.0.0.1';
GRANT ALL PRIVILEGES ON $DB.* TO '$DBUSER'@'localhost';
FLUSH PRIVILEGES;
SQL
  umask 027
  cat > "$ETC/license.env" <<EOF
FLOVMP_LA_DB=Server=127.0.0.1;Port=3306;Database=$DB;User ID=$DBUSER;Password=$PASS
FLOVMP_LA_KEY=$ETC/authority.pem
FLOVMP_LA_DATA=$DATA
FLOVMP_LA_LISTEN=http://127.0.0.1:7800/
EOF
  chown root:flovmp-license "$ETC/license.env"; chmod 0640 "$ETC/license.env"
fi
# 7799 принадлежит игровому API FloV:MP. Мигрируем только прежнее значение
# по умолчанию, не перезаписывая явно выбранный владельцем нестандартный порт.
sed -i 's|^FLOVMP_LA_LISTEN=http://127\.0\.0\.1:7799/$|FLOVMP_LA_LISTEN=http://127.0.0.1:7800/|' "$ETC/license.env"

cat > /usr/local/bin/flovmp-license <<'EOF'
#!/bin/sh
# Команды выполняются от пользователя службы: права на файлы не ломаются.
if [ "$(id -u)" -eq 0 ]; then exec runuser -u flovmp-license -- dotnet /opt/flovmp-license/flovmp-license.dll "$@"; fi
exec dotnet /opt/flovmp-license/flovmp-license.dll "$@"
EOF
chmod 0755 /usr/local/bin/flovmp-license
flovmp-license init | tail -n +1

echo "==> служба"
# Прежняя Python-раздача (flovmp-dist) заменена этой службой.
if systemctl list-unit-files | grep -q '^flovmp-dist.service'; then
  systemctl disable --now flovmp-dist >/dev/null 2>&1 || true
  rm -f /etc/systemd/system/flovmp-dist.service
fi
cat > /etc/systemd/system/flovmp-license.service <<EOF
[Unit]
Description=FloV:MP — сервер лицензий и раздача пакетов
After=network.target mariadb.service mysql.service

[Service]
User=flovmp-license
Group=flovmp-license
ExecStart=$DOTNET $APP/flovmp-license.dll serve
Restart=always
RestartSec=3
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
ReadWritePaths=$DATA

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable flovmp-license >/dev/null
systemctl restart flovmp-license

echo "==> nginx"
# Общий /api/ уже принадлежит игровому API на 127.0.0.1:7799. Только четыре
# префикса лицензирования отправляем в отдельную службу на 127.0.0.1:7800.
# Внутренняя папка нужна для X-Accel-Redirect после проверки ключа.
# Копии конфига — вне sites-enabled: nginx читает оттуда все файлы подряд.
install -d -m 0700 /root/nginx-backups
# Предыдущий прерванный запуск мог оставить в sites-enabled только резервную
# копию. Сначала восстанавливаем рабочее имя, иначе перенос копий отключит сайт.
if [ ! -e "$SITE" ]; then
  RECOVERY="$(find /etc/nginx/sites-enabled -maxdepth 1 -type f -name 'default.bak-*' -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -1 | cut -d' ' -f2-)"
  if [ -n "$RECOVERY" ] && [ -f "$RECOVERY" ]; then
    cp "$RECOVERY" "$SITE"
  elif [ -f /etc/nginx/sites-available/default ]; then
    ln -s /etc/nginx/sites-available/default "$SITE"
  else
    echo "нет nginx-конфига $SITE и нет копии для восстановления"
    exit 1
  fi
fi
for old in /etc/nginx/sites-enabled/*.bak-*; do [ -e "$old" ] && mv "$old" /root/nginx-backups/; done
if [ -f "$SITE" ] && { ! grep -q "_flovmp_dist_files" "$SITE" || ! grep -q "_flovmp_license_api" "$SITE"; }; then
  cp "$SITE" "/root/nginx-backups/default.bak-license-$(date +%s)"
  python3 - "$SITE" <<'PY'
import sys
p = sys.argv[1]
s = open(p, encoding="utf-8").read()
anchor = "    location /api/ {"
if anchor not in s:
    raise SystemExit("не найден nginx location /api/ для вставки маршрутов лицензирования")
blocks = ""
if "_flovmp_dist_files" not in s:
    blocks += """    location /_flovmp_dist_files/ {
        internal;
        alias /var/lib/flovmp-license/releases/current/;
        sendfile on;
        tcp_nopush on;
        add_header Cache-Control "no-store" always;
    }
"""
if "_flovmp_license_api" not in s:
    blocks += """    # _flovmp_license_api: лицензирование отдельно от игрового API :7799
    location ^~ /api/v1/license/ {
        limit_req zone=flovmp_api burst=20 nodelay;
        limit_conn flovmp_conn 12;
        client_max_body_size 16k;
        proxy_pass http://127.0.0.1:7800;
        proxy_set_header Host 127.0.0.1;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_connect_timeout 3s;
        proxy_read_timeout 10s;
    }
    location ^~ /api/v1/licenses/ { proxy_pass http://127.0.0.1:7800; proxy_set_header X-Real-IP $remote_addr; proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; }
    location ^~ /api/v1/distribution/ { proxy_pass http://127.0.0.1:7800; proxy_set_header X-Real-IP $remote_addr; proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; }
    location = /api/v1/telemetry/heartbeat { proxy_pass http://127.0.0.1:7800; proxy_set_header X-Real-IP $remote_addr; proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; }

"""
i = s.index(anchor)
open(p, "w", encoding="utf-8").write(s[:i] + blocks + s[i:])
PY
fi
nginx -t && systemctl reload nginx

install -d -m 0755 /var/www/cdn
for f in get.sh get.ps1 flovmp-setup.zip; do [ -f "$SRC/$f" ] && install -m 0644 "$SRC/$f" "/var/www/cdn/$f"; done
# Раздача загрузчиков и архива установки: их клиент качает до всякой лицензии.
if [ -f "$SITE" ] && ! grep -q "location /cdn/" "$SITE"; then
  cp "$SITE" "/root/nginx-backups/default.bak-cdn-$(date +%s)"
  python3 - "$SITE" <<'CDN'
import sys
p = sys.argv[1]
s = open(p, encoding="utf-8").read()
anchor = "    location /api/ {"
block = """    # _flovmp_cdn: get.sh, get.ps1 и flovmp-setup.zip
    location /cdn/ {
        alias /var/www/cdn/;
        autoindex off;
        add_header Cache-Control "no-store" always;
    }

"""
i = s.index(anchor)
open(p, "w", encoding="utf-8").write(s[:i] + block + s[i:])
CDN
  nginx -t && systemctl reload nginx
fi

sleep 2
curl -fsS http://127.0.0.1/api/v1/license/health && echo
echo "Готово. Выдать ключ: flovmp-license new --project \"Проект\" --owner \"Имя\" --days 365"
