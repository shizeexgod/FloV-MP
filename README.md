# FloV:MP & Держава Онлайн

Автономный мультиплеерный движок нового поколения (**FloV:MP**) + игровой RP-проект (**Держава Онлайн**) с фирменным лаунчером и веб-порталом управления лицензиями.

> RAGE:MP и alt:V закрыты Take-Two в 2026. FloV:MP — независимая суверенная платформа для GTA V, работающая автономно без обращения к чужой инфраструктуре.

Полный контекст и архитектурные решения — в [`CLAUDE.md`](CLAUDE.md) и [`AGENTS.md`](AGENTS.md).  
Текущий статус и дорожная карта — в [`agent_state.md`](agent_state.md).

## Стек технологий

| Слой | Технология | Описание |
|---|---|---|
| **Сетевой рантайм** | C++ (unhooked alt:V core) | UDP порт 7788, FastDL CDN Nginx, 3D Voice WebRTC |
| **Сервер (гейм-логика)** | C# / .NET 8 (`coreclr-module`) | MariaDB 10.6 пул, экономика, транспорт, 8-уровневая админка |
| **Лаунчер** | Electron (Chromium UI) + C# Native | Аппаратное ускорение, кастомные акценты, безопасный запуск |
| **Клиентский UI/HUD** | HTML5 / React / TypeScript (CEF) | NUI-интерфейсы с 60+ FPS |
| **Веб-портал (SaaS)** | Next.js 14 / Tailwind / TypeScript | Личный кабинет, биллинг, генерация лаунчеров, серверная оффлайн-админка |

## Структура репозитория

```
FloV-MP/
├── server/       — C#-гейммод (FloVMP.Core, FloVMP.Gamemode) + unit-тесты
├── client/       — Клиентский игровой UI-ресурс (flovmp-client)
├── launcher/     — Лаунчер проекта:
│   ├── electron/ — Основной UI лаунчера (Chromium, HTML5, CSS)
│   └── src/      — Нативный C# помощник (Connect, Launcher.Native, Core)
├── web/          — SaaS веб-портал (Next.js 14, ЛК, биллинг, верификация лицензий, оффлайн Control Plane)
├── sql/          — Схемы MariaDB (игровая schema.sql, портальная portal_schema.sql)
├── config/       — server.toml и профили подключения
├── scripts/      — Утилиты запуска и обслуживания (run-launcher, run-server, connect)
├── runtime/      — Локальный рантайм alt:V (бинарники в git не идут)
├── logs/         — Логи запусков и отладки
├── archive/      — Архив устаревших прототипов (старый WPF-лаунчер)
└── docs/         — Архитектурная и техническая документация
```

## Быстрый запуск

### 1. Запуск лаунчера
```cmd
scripts\run-launcher.cmd
```

### 2. Запуск локального сервера
```cmd
scripts\run-server.cmd
```

### 3. Быстрое прямое подключение к серверу
```cmd
scripts\connect.cmd
```
*(По умолчанию подключается к выделенному серверу `188.127.229.224:7788`)*

### 4. Запуск SaaS веб-портала
```cmd
cd web
npm run dev
```
*(Доступен по адресу `http://localhost:3000`)*

## Тестирование и верификация

```powershell
# Тесты серверного ядра (229 тестов, 100% green)
dotnet test server/tests/FloVMP.Core.Tests/FloVMP.Core.Tests.csproj

# Тесты нативного лаунчера (41 тест, 100% green)
dotnet test launcher/tests/FloVMP.Launcher.Tests/FloVMP.Launcher.Tests.csproj

# Суммарно: 270 автоматических тестов пройдено

# Сборка веб-портала (44 роута)
cd web; npm run build
```
