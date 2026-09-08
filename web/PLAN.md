# FloV:MP SaaS Web Portal — план реализации

Концепт **01 (Premium SaaS, `/concepts/saas`)** переносится в боевой сайт `web/` как
полноценный многостраничный SaaS-продукт: публичный сайт + личный кабинет + API.

## Архитектура (решение)

| Слой | Решение | Почему |
|---|---|---|
| Хостинг фронта | **Vercel** (Next.js 14 App Router) | по требованию владельца |
| Бэкенд | **Next.js API Routes на том же Vercel** (serverless / node runtime), SSE через streaming Response | не нужен отдельный сервис, всё в одном деплое |
| БД (прод) | **MariaDB на VDS `188.127.229.224`** через `mysql2/pool` + `DATABASE_URL` env | БД уже есть; Vercel FS только `/tmp` |
| БД (локально) | fallback `data/portal-db.json` (как сейчас) | офлайн-разработка |
| Файлы сборок лаунчера / FastDL | S3-совместимое (Cloudflare R2) или VDS `nginx` | Vercel FS read-only |
| i18n | свой лёгкий провайдер (RU/EN), без next-intl | нет доступа к npm-инсталлу в среде; переключатель RU/EN сверху |

## Публичный сайт — отдельные страницы (не лендинг-одностраничник)

| Route | Назначение |
|---|---|
| `/` | Хиро, ключевые возможности, метрики платформы, проекты, CTA |
| `/features` | Глубокое описание: движок, txAdmin Cloud, Resource Manager, телеметрия, FloV:ID, Launcher Builder, Voice, FastDL |
| `/pricing` | Тарифы Starter / Pro / Enterprise, переключатель период/скидки, FAQ по биллингу, партнёрка 20% |
| `/projects` | Витрина проектов на FloV:MP (Держава Онлайн, Florida V, Sayonara RP) + публичный статус |
| `/roadmap` | Дорожная карта по кварталам |
| `/docs` | Документация (уже есть — переоформить в общий стиль) |
| `/docs/quickstart` | Первый запуск сервера: порт 7788, F8, команды `/o /pos /setdim /veh /stats`, баны |
| `/contact` | Telegram, Discord, форма заявки |
| `/legal/terms`, `/legal/privacy` | Юридические |
| `/auth/login`, `/auth/register` | Вход/регистрация (есть — переоформить) |

## Личный кабинет `/dashboard` — по ТЗ Gemini (14 модулей)

Разбить монолит `dashboard/page.tsx` на `components/dashboard/*Tab.tsx`:
Overview, Projects, Servers, ResourceManager, Watchdog, Analytics/Telemetry,
TxAdminConsole (SSE), Logs, LauncherBuilder, Billing, ApiWebhooks, SDK, AiAssistant, Settings(2FA/RBAC/реферал).

## Бэкенд — что углубляем

- `lib/db.ts` — параметризованные запросы, типизация OkPacket, пул + fallback (есть, дочистить).
- `lib/auth.ts` — HttpOnly cookie сессии (есть) + 2FA TOTP (RFC 6238) + RBAC (Owner/Admin/ProjectLead/Developer).
- `lib/license.ts` — `FLV-XXXX-XXXX-XXXX`, HMAC-SHA256 подпись, авто-привязка IP, grace period 7 дней (есть — дополнить).
- `api/v1/agent/{poll,command,stream}` — очередь команд + SSE (есть — довести).
- `lib/webhookDispatcher.ts` — Discord Embeds + Telegram (есть).
- Все роуты с `req.json()` — `try/catch` → `400`.

## Требования к качеству

- `npx tsc --noEmit` → 0 errors.
- `npm run build` → все роуты без ошибок/варнингов.
- Каждая кнопка/ссылка ведёт на реальный маршрут.
- Кнопки анимированы (hover/active/focus), блоки выровнены по единой сетке.
- RU по умолчанию; в RU нет английского, в EN нет русского.
- C# тесты ядра/лаунчера не трогаем.
- `git push` — только по явной команде владельца.

## Фазы

1. ✅ **i18n + дизайн-система + многостраничная навигация** — сделано (RU/EN,
   переключатель, self-hosted шрифты, анимированные кнопки, reveal без залипаний)
2. ✅ **Наполнение страниц** — home/features/pricing/projects/roadmap/contact/docs/legal
   + not-found; все ссылки ведут на реальные маршруты; интерактив (тариф-периоды,
   FAQ, форма, docs verify/SSE) протестирован
3. ✅ **Личный кабинет + админ-панель** — все 9 вкладок + 5 модалок, подключены
   к реальным API, чистый стиль, **полная i18n RU/EN** (dict.ts: dash.* / adm.*).
   **Декомпозиция выполнена**: `dashboard/page.tsx` 2657 → 828 строк — контейнер
   держит состояние/хендлеры/загрузку, отдаёт через `DashboardProvider` (context).
   `components/dashboard/`: `_ctx.tsx` + 9 вкладок + 5 модалок = 15 файлов.
   Поведение не менялось (JSX перенесён дословно), проверено в браузере RU и EN.
4. 🔲 **Бэкенд**: 2FA TOTP, RBAC, grace period, agent poll-очередь, публичный API HMAC
5. 🟡 **Vercel**: `vercel.json` + `.env.example` готовы. Осталось: миграция
   файлового стора (portal-db.json / сборки лаунчера) на R2/VDS, т.к. FS Vercel r/o

## Безопасность (исправлено)
- `/api/auth/me` больше не отдаёт `password_hash` (JSON-fallback игнорировал SELECT)
