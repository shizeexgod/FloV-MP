# PROMPT ДЛЯ CLAUDE CODE: ПОЛНАЯ АРХИТЕКТУРА И СПЕЦИФИКАЦИЯ ДЭШБОРДА FLOV:MP

> **Назначение документа:** Этот файл является прямым техническим заданием (System Prompt) для Claude Code. Он описывает финальную, утверждённую владельцем структуру дашборда SaaS-платформы FloV:MP (`c:\FloV-MP\web`), дизайн-систему, карту экранов, контракты API и компоненты.

---

## 🎯 РОЛЬ И КОНТЕКСТ

Ты выступаешь как **Principal Frontend Architect & UI/UX Design Engineer**.
Твоя задача — реализовать и довести до идеала веб-интерфейс SaaS-платформы FloV:MP (`/concepts/hybrid/app` и `/dashboard`), ориентируясь на премиальный уровень Majestic RP / GTA5RP / Vercel / Linear.

**Стек:**
- Next.js 14 (App Router)
- React 18 / TypeScript
- Tailwind CSS
- Lucide React (иконки)
- Векторная графика SVG (графики без тяжелых внешних библиотек)

---

## 🎨 ДИЗАЙН-СИСТЕМА (CYBER DARK GLASS)

```css
/* Цветовая палитра */
--accent-pink: #ff3d8a;      /* Codex Neon Pink (основной акцент) */
--accent-purple: #a855f7;    /* Cyber Violet (вторичный градиент) */
--bg-black: #08080c;         /* Глубокий фон */
--bg-surface: #0e0e14;       /* Подложка панелей */

/* Glassmorphism Card Style */
.hyapp-card {
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.045), rgba(255, 255, 255, 0.015));
  border: 1px solid rgba(255, 255, 255, 0.09);
  backdrop-filter: blur(18px) saturate(140%);
  border-radius: 14px;
}

/* Accent Glow Border */
.hyapp-glow {
  box-shadow: 0 0 25px rgba(255, 61, 138, 0.18);
}
```

---

## 🧭 УТВЕРЖДЁННАЯ СТРУКТУРА МЕНЮ (14 МОДУЛЕЙ)

Навигационная панель (Sidebar) должна содержать следующие модули в строгом порядке:

```typescript
export const NAV_ITEMS = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'projects', label: 'Projects', icon: Boxes },
  { id: 'servers', label: 'Servers', icon: Server },
  { id: 'resources', label: 'Resources', icon: FolderTree },        // Dynamic Resource Manager
  { id: 'watchdog', label: 'Watchdog & Crashes', icon: ShieldAlert },// Freeze & Crash Supervisor
  { id: 'analytics', label: 'Analytics', icon: Activity },
  { id: 'console', label: 'Console (txAdmin)', icon: Terminal },
  { id: 'logs', label: 'Logs', icon: Database },
  { id: 'launcher', label: 'Launcher Builder', icon: Cpu },         // Custom Exe Generator
  { id: 'billing', label: 'Billing & Invoices', icon: CreditCard },  // SaaS Plans & Payments
  { id: 'api', label: 'API & Webhooks', icon: KeyRound },
  { id: 'sdk', label: 'SDK & FastDL', icon: Download },
  { id: 'integrations', label: 'Integrations', icon: Plug },
  { id: 'ai', label: 'AI Assistant', icon: Sparkles },
  { id: 'settings', label: 'Settings', icon: Settings },
];
```

---

## 📱 ДЕТАЛЬНАЯ СПЕЦИФИКАЦИЯ КАЖДОГО МОДУЛЯ

### 1. Dashboard (Сводная панель)
- **Top Quick Bar:** Кнопка быстрого вызова **«Глобальное сообщение (/o)»** — открывает модальное окно для моментальной трансляции объявления на весь игровой сервер через SSE/Agent.
- **KPI-плитки:**
  - Суммарный онлайн игроков (с индикатором роста `+14% к прошлой неделе`).
  - Пиковый онлайн за 24 часа.
  - Средний Tickrate (60.0 Hz) и Engine FPS (60.0 FPS).
  - Статус лицензии (`Enterprise Lifetime`, `Active`).
- **Интерактивный график 24h:** Онлайн, CPU, RAM с переключением метрик.
- **Мини-таблица активных серверов:** Имя, среда (Prod/Dev/Test), онлайн/слоты, мини-бары загрузки.
- **Лента быстрых алертов:** Предупреждения о пиках нагрузки и событиях безопасности.

### 2. Projects (Проекты и Безопасность FloV:ID)
- **Карточки проектов:** Переключение активного проекта (`Florida V`, `Sayonara RP`, `Держава Онлайн`).
- **Команда проекта:** Список участников с ролями (`Owner`, `Administrator`, `Developer`) и кнопкой приглашения.
- **Панель настроек безопасности FloV:ID & HWID Policy:**
  - Выбор режима:
    - `Strict` — Полная блокировка забаненного железа и обходчиков.
    - `Lenient` — Мягкий режим (логирование попыток входа без кика для серверов на старте с дефицитом онлайна).
    - `Disabled` — Отключение проверки железа.
  - Тумблер `Allow VPN` (разрешать ли подключение через прокси/VPN).
  - Слайдер `Лимит аккаунтов на один HWID` (от 1 до 10).

### 3. Servers (Серверные среды)
- Вкладки сред: **Production** (боевой VDS), **Development** (локальная среда разработки), **Test** (песочница).
- Для каждого сервера карточка:
  - Публичный IP и порт (например, `188.127.229.224:7788`).
  - Статус: `Online` (зелёный пульсирующий бейдж), `Offline`, `Deploying`.
  - Метрики реального времени: CPU %, RAM %, Tickrate (60.0 Hz), Uptime.
  - Кнопки быстрого управления: `Start`, `Stop`, `Restart`, `Перейти в консоль`.

### 4. Resources (Менеджер игровых ресурсов)
> *Аналог txAdmin / fxserver dynamic resource manager, управляемый через `DynamicResourceManager.cs`.*
- Таблица скриптов, карт, паков машин и UI:
  - Название ресурса (например: `flovmp-core`, `derzhava-moscow-map`, `derzhava-vehicles-pack`).
  - Тип: `Script`, `Map`, `VehiclePack`, `UI`.
  - Статус: `Running` / `Stopped` / `Failed`.
  - Зависимости (`Dependencies`): бейджи ресурсов, необходимых для запуска.
  - Потребление ОЗУ (MB).
  - Действия: Кнопки **«Старт»**, **«Стоп»**, **«Перезапуск»** без выключения сервера!

### 5. Watchdog & Crashes (Авторестарт и расследование аварий)
- Монитор супервизора `ServerCrashWatchdog.cs`:
  - Детекция зависаний основного потока (порог: > 15 секунд).
  - Статус авторестарта: `Активен (Лимит: макс 5 рестартов/час)`.
- Таблица инцидентов (`portal_server_crashes`):
  - ID инцидента (`INC-4091`).
  - Сервер, время, тип (`Freeze (>15s)`, `CoreCLR Exception`, `Memory Leak`).
  - Кликабельный просмотр стека вызова (Call Stack Trace) с подсветкой упавшей строки C#.

### 6. Analytics (Аналитика и SLA)
- Детальные 24-часовые графики временных рядов (Time-series):
  - Онлайн игроков по часам с отметкой пиков.
  - Загрузка CPU узла.
  - Потребление RAM пула CoreCLR.
  - Сетевой трафик (Mbps входящий/исходящий).
- Бейдж верифицированной доступности: **99.98% SLA**.

### 7. Console (txAdmin Cloud Terminal)
- Выбор сервера.
- **SSE Live Log Stream:** Переключатель прямого подключения к Server-Sent Events (`/api/v1/agent/stream`) без задержек.
- Интерактивный терминал с цветовой подсветкой:
  - Синий — `[INFO]`, Зелёный — `[OK]`, Жёлтый — `[WARN]`, Красный — `[ERROR]`, Пурпурный — `[SECURITY]`.
- Инпут отправки RCON-команд на игровой сервер.

### 8. Logs (Системный журнал событий)
- Фильтрация по уровням (Все, INFO, WARN, ERROR, CRASH).
- Поиск по тексту и IP.
- Экспорт лога в CSV / JSON.

### 9. Launcher Builder (Конструктор фирменного лаунчера)
- Поля настройки бренда:
  - Название проекта (например, «Держава RP»).
  - Выбор акцентного цвета (палитра + HEX-код: `#ff3d8a`, `#00e5ff`, `#a855f7` и т.д.).
  - Загрузка логотипа и промо-баннера.
  - Привязка IP и UDP порта сервера.
- Кнопка **«Скомпилировать установщик»** — генерирует готовый `Setup.exe` со встроенным CDN-манифестом.

### 10. Billing & Invoices (Счета и лицензии)
- Карточки тарифов: Starter (128 слотов), Pro (500 слотов), Enterprise (1500+ слотов).
- Скидочная сетка: **-15% на 6 мес.**, **-30% на 1 год**.
- Таблица счетов (`INV-2026-XXXX`): сумма, тариф, дата, статус (Оплачен / Ожидает), скачивание чека.

### 11. API & Webhooks (Интеграции и Донат)
- API Ключи: `flv_live_...`, `flv_pub_...` с маскированием, копированием и ротацией.
- Вебхуки уведомлений:
  - Discord Webhook URL (с кнопкой «Отправить тестовый Rich Embed»).
  - Telegram Bot Token & Chat ID.
- **E-Commerce Webhook (Донат):**
  - Эндпоинт `/api/v1/public/[slug]/donate` с HMAC-SHA256 верификацией для моментальной выдачи доната и валюты игрокам онлайн.

### 12. SDK & FastDL
- Раздача дистрибутивов: Server Core (Linux/Win), C# SDK, CLI Asset Packer.
- Интерактивные вкладки с примерами кода (C#, TypeScript, cURL).

### 13. AI Assistant (Нейро-диагностика сбоев)
- Чат-интерфейс в стиле ChatGPT/Linear.
- Готовые пресеты запросов: «Почему упал тикрейт?», «Разбери краш-дамп INC-4091», «Оптимизируй MySQL пул».

### 14. Settings (Настройки аккаунта)
- Профиль владельца.
- 2FA / TOTP защита входа (QR-код для Google Authenticator).
- Партнёрская программа: промокод, скидка 10% для рефералов, 20% пожизненных отчислений владельцу.

---

## 🚀 ИНСТРУКЦИЯ ПО РЕАЛИЗАЦИИ ДЛЯ CLAUDE CODE

1. При реализации компонентов держи структуру файлов в `web/src/app/`:
   - `/concepts/hybrid/app/page.tsx` — интерактивный прототип с полным набором данных.
   - `/dashboard/page.tsx` — боевая страница кабинета с подключением к Next.js API.
2. Не ломай существующий контракт типов в `web/src/app/concepts/_appdata.ts`.
3. Все интерактивные действия (кнопки Старт/Стоп ресурсов, отправка /o объявления, переключение политик HWID) должны давать мгновенный визуальный фидбек (toast-уведомление или индикатор загрузки).
4. Строго соблюдай тёмную неоновую палитру (`#ff3d8a`, `#a855f7`, фон `#08080c`).
