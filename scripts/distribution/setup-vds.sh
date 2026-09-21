#!/usr/bin/env bash
# FloV:MP — установка сервиса раздачи пакетов на VDS платформы (один раз;
# повторный запуск обновляет код сервиса, ключи и релизы не трогает).
#
#   sudo bash setup-vds.sh <папка с flovmp_dist.py, get.sh, get.ps1>
#
# Ставит: /usr/local/lib/flovmp-dist/flovmp_dist.py, команду flovmp-dist,
# службу flovmp-dist (127.0.0.1:7799, пользователь flovmp-dist), в nginx —
# internal-location для отдачи файлов, загрузчики в /var/www/cdn.
set -euo pipefail
SRC="${1:-.}"
DATA=/var/lib/flovmp-dist
LIB=/usr/local/lib/flovmp-dist
SITE=/etc/nginx/sites-enabled/default

[ "$(id -u)" -eq 0 ] || { echo "запустите через sudo"; exit 1; }
for f in flovmp_dist.py get.sh get.ps1; do [ -f "$SRC/$f" ] || { echo "нет $SRC/$f"; exit 1; }; done
command -v python3 >/dev/null || { echo "нужен python3"; exit 1; }

id flovmp-dist >/dev/null 2>&1 || useradd --system --home-dir "$DATA" --shell /usr/sbin/nologin flovmp-dist
install -d -m 0755 "$LIB"
install -m 0644 "$SRC/flovmp_dist.py" "$LIB/flovmp_dist.py"
install -d -m 0755 -o flovmp-dist -g flovmp-dist "$DATA" "$DATA/releases"
# keys.json — только владельцу службы; пакеты — на чтение nginx (www-data).
[ -f "$DATA/keys.json" ] || { echo '{}' > "$DATA/keys.json"; }
chown flovmp-dist:flovmp-dist "$DATA/keys.json"; chmod 0600 "$DATA/keys.json"

cat > /usr/local/bin/flovmp-dist <<'EOF'
#!/bin/sh
# Команды раздачи выполняются от пользователя службы: права на keys.json не ломаются.
if [ "$(id -u)" -eq 0 ]; then exec runuser -u flovmp-dist -- python3 /usr/local/lib/flovmp-dist/flovmp_dist.py "$@"; fi
exec python3 /usr/local/lib/flovmp-dist/flovmp_dist.py "$@"
EOF
chmod 0755 /usr/local/bin/flovmp-dist

cat > /etc/systemd/system/flovmp-dist.service <<'EOF'
[Unit]
Description=FloV:MP — раздача серверных пакетов по ключу лицензии
After=network.target

[Service]
User=flovmp-dist
Group=flovmp-dist
Environment=FLOVMP_DIST_LISTEN=127.0.0.1:7799
ExecStart=/usr/bin/python3 /usr/local/lib/flovmp-dist/flovmp_dist.py serve
Restart=always
RestartSec=3
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
ReadWritePaths=/var/lib/flovmp-dist

[Install]
WantedBy=multi-user.target
EOF

# nginx: файл отдаёт сам nginx после разрешения сервиса (X-Accel-Redirect).
if [ -f "$SITE" ] && ! grep -q "_flovmp_dist_files" "$SITE"; then
  cp "$SITE" "$SITE.bak-dist-$(date +%s)"
  python3 - "$SITE" <<'PY'
import sys
p = sys.argv[1]
s = open(p, encoding="utf-8").read()
block = """    location /_flovmp_dist_files/ {
        internal;
        alias /var/lib/flovmp-dist/releases/current/;
        sendfile on;
        tcp_nopush on;
        add_header Cache-Control "no-store" always;
    }
    location /api/v1/distribution/ {
        limit_req zone=flovmp_api burst=20 nodelay;
        limit_conn flovmp_conn 12;
        proxy_pass http://127.0.0.1:7799;
        proxy_set_header Host 127.0.0.1;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_connect_timeout 3s;
        proxy_read_timeout 30s;
    }
"""
i = s.index("    location /api/ {")
s = s[:i] + block + s[i:]
open(p, "w", encoding="utf-8").write(s)
PY
fi
nginx -t
systemctl daemon-reload
systemctl enable --now flovmp-dist >/dev/null
systemctl restart flovmp-dist
systemctl reload nginx

install -d -m 0755 /var/www/cdn
install -m 0644 "$SRC/get.sh" /var/www/cdn/get.sh
install -m 0644 "$SRC/get.ps1" /var/www/cdn/get.ps1
sleep 1
curl -fsS http://127.0.0.1/api/v1/distribution/health && echo
echo "Готово. Ключи: flovmp-dist keys add FLV-XXXX-XXXX-XXXX --note \"Проект\"; релиз: flovmp-dist publish <папка>"
