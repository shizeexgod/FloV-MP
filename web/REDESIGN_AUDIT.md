# FloV:MP — REDESIGN_AUDIT.md

Phase 0 audit per `FLOVMP_WEBSITE_REDESIGN_CLAUDE_PROMPT.md`. Based on a fresh read of the live tree (`web/`) as of this commit, not memory.

## 1. Что уже работает и стоит сохранить

- **Шрифты правильные**: Unbounded (display) + Manrope (текст) + JetBrains Mono, самохостинг woff2, подключены глобально в `globals.css`. Мастер-промпт просит "clean modern sans, not gaming display font" — Unbounded технический и сдержанный, не «игровой» дисплей-шрифт типа Orbitron; оставляем как есть, менять не нужно.
- **Токены есть, но не централизованы явно**: `--bg/--panel/--line/--brand/--brand-hi/--brand-lo/--text/--ease/--ease-back` в `:root` globals.css. По сути уже соответствует требуемому списку токенов (п. 38 промпта), просто не названы `surface/surface-elevated/text-secondary` и т.д. — переименовывать не обязательно, framework на месте.
- **Мотор motion уже частично построен**: `<Reveal variant="left|right|fade|rise|scale">` в `site.tsx`, `.icon-pop` (hover bounce), `.hover-text` (лёгкий scale ссылок), `--ease`/`--ease-back` bezier — это ровно тот словарь анимаций, который просит мастер-промпт (fade+translate вход, mask reveal, stagger). Не нужно изобретать новую систему — донастроить существующую.
- **`.browser-frame`**: компонент-рамка браузера (macOS-точки + url-бар) вокруг реальных PNG-скриншотов кабинета (`/media/shot-dashboard-*.png`) — это уже "real dashboard screenshots, not fake SaaS UI" (п. 13, 45). Хорошая база для секции dashboard-mockup, просто сейчас используется не в 3D/перспективе, а плоско, и не в самой сильной позиции на странице.
- **Реальные данные, не выдумка**: `/projects` — честный empty-state вместо фейковых логотипов/цифр; hero не содержит fake-статистику (win: п. 25 "Do not invent capabilities/fake statistics" уже соблюдён).
- **RU/EN i18n工作** полностью сквозной, `dict.ts` зеркалит RU→EN одной структурой, `useT()`/`I18nProvider` — крепкий фундамент, редизайн ломать не должен.
- **Дашборд только что причёсан** (см. `web/src/app/dashboard/page.tsx`, `NoProjectGate.tsx`) — рамки-разделители инсетные (`.rule-soft`), кастомный `<Select>` вместо системного `<select>`, единая точка покупки лицензии/создания проекта, цветовая палитра сведена к монохрому+розовому. Этот прогон НЕ трогаем в рамках текущего публичного редизайна — просит сам мастер-промпт («не ломать dashboard links»).

## 2. Что выглядит типично / generic (главные проблемы)

1. **Hero = абстрактный 3D-блоб, не GTA-визуал.** `EngineCore` в `page.tsx` + `.engine-core*` в globals.css (118 строк CSS) — светящаяся аура, орбиты-эллипсы, перспективная сетка и буквы «F:M» в рамке. Это ровно то, что промпт прямым текстом запрещает: *"random abstract 3D cube for no reason", "meaningless glowing particles", "crypto-looking artifacts", "generic 3D blob"*. Игровая идентичность в hero отсутствует полностью — по тексту сайт про GTA V, по картинке — про нечто среднее между NFT-лендингом и AI-стартапом.
2. **Header всё ещё содержит язык.** `Navbar.tsx`: `<LangSwitch />` стоит прямо в правом блоке хедера рядом с логином, до сих пор видим RU/EN пилюлю на каждой странице. Промпт: *"no language selector in the header… move to footer"*.
3. **Хедер держит два «центра тяжести» действий**: пилюля логина/кабинета + отдельная кнопка-гамбургер (тема). Не критично, но можно сжать до одного action-блока как в RAGE:MP (просто "Войти").
4. **Footer выглядит как контакты фрилансера, не платформы.** Колонка «Контакты» = личный Telegram + личный телефон + email, и внизу футера отдельно ещё раз Telegram-ссылка с иконкой. Промпт прямо: *"do not prominently feature personal phone number… do not present the footer like contact-me-personally"*. Нужно: юр.реквизиты — в /legal/requisites (уже есть страница), а видимый футер строить вокруг Support/Docs/Terms/Privacy/Product, без личного номера на первом экране футера.
5. **Языка в футере нет вовсе** — сейчас переключение возможно только в хедере/бургере. Нужно добавить явный языковой блок внизу (RU/EN текстом, без флагов) и убрать из хедера.
6. **Повторяющаяся визуальная грамматика "card card-hover p-6" почти в каждой секции** (`features`, `quickstart`, `ownership`, финальный CTA) —6+ одинаковых карточных блоков подряд вниз по странице, именно "repetitive card grids" из п. 12/20. Одна секция (Pillars/zigzag, `ProductPreview`) уже editorial по духу — остальные плоские.
7. **`ProductPreview` (макет кабинета в блоке Pillars) плоский, без перспективы/depth** — сейчас это `.browser-frame` 1-в-1 такой же, как в hero-скриншотах (используется трижды подряд с разными `mode`). Нет ощущения "объекта", только повторяющаяся рамка.
8. **`dashboard-concepts/` — пустая директория** (0 файлов) в `src/components/` — мёртвый след предыдущей итерации, тянуть в редизайн не нужно, можно удалить в Phase 2.
9. **Reveal-система есть, но `page.tsx` использует её не везде равномерно** — часть блоков (`quickstart`, `ownership`) без `variant`, то есть только вертикальный fade, тогда как zigzag-блоки уже используют `left/right/scale`. Несогласованность, а не отсутствие системы.

## 3. Что остаётся без изменений (по требованию промпта, п. 40/41)

- Auth-флоу, роуты (`/auth/login`, `/auth/register`, `/dashboard`, `/docs`, `/pricing`, `/contact`, `/legal/*`, `/projects`, `/features`, `/admin`).
- `I18nProvider`/`useT()`, вся структура `dict.ts` (RU⇄EN), backend API-роуты.
- Личный кабинет (`/dashboard/*`) — уже отдельно причёсан этой же сессией, out of scope здесь.
- Продуктовый смысл текста (лицензия Lifetime, автономный движок, FastDL, HWID/анти-VPN и т.д.) — можно сокращать/уплотнять формулировки, но не выдумывать новые возможности.
- `ThemeMenu`/`useTheme()` (свет/тьма) технически остаётся (уже есть переключатель light/dark), но по п. 30 не должен быть на видном месте хедера — держим доступным, но не как первичный элемент.

## 4. Переиспользуемые компоненты (Phase 2+ строим поверх них, не с нуля)

| Компонент | Файл | Статус |
|---|---|---|
| `Reveal` (fade/left/right/rise/scale) | `src/components/site.tsx` | Готов, донастроить (stagger, mask-reveal для крупных картинок) |
| `Section` / `SectionHeading` | `src/components/site.tsx` | Готов, но `bordered` даёт full-bleed `.rule` — ок, уже инсет |
| `.browser-frame` | `globals.css` | Готов под dashboard-скриншоты |
| `Select` (кастомный dropdown) | `src/components/ui.tsx` | Готов, единый стиль вылезающих списков по всему проекту |
| `BtnLink` / `.btn` | `site.tsx` + `globals.css` | Готов: sheen-hover, brightness(1.06), стрелка — не трогать |
| `LangSwitch` | `src/components/LangSwitch.tsx` | Переносим по месту (из хедера в футер), логику не переписываем |
| `ThemeMenu` / `ThemeControls` | `src/components/ThemeMenu.tsx` | Оставляем, убираем из первичного header-flow |
| Реальные скрины кабинета | `public/media/shot-dashboard-*.png` | Единственный реальный "product visual" на сайте — обязательно использовать в dashboard-mockup секции |

## 5. Что нужно удалить/выключить

- `.engine-core*` CSS-блок и `<EngineCore/>` компонент — заменяется GTA-хиро-визуалом.
- `src/components/dashboard-concepts/` — пустая папка, удалить.
- `<LangSwitch/>` из `Navbar.tsx` (оба места: десктоп-хедер и мобильный sheet).
- Личный телефон/handle Telegram как заглавный контакт в футере — уводим в `/legal/requisites` (уже есть), в футере оставляем безличные Support/Docs.

## 6. Ассеты — инвентарь

`public/branding/`: логотип (`logo.png`, актуальный), плюс россыпь исторических черновиков (`hero-showcase.png`, `logo-banner-1/2.png`, «Изображение Codex …» — 7 файлов, ~7 МБ суммарно) — старые эксперименты, GTA-art среди них нет, использовать нельзя (не тот стиль/не хиро-композиция).
`public/media/`: 3 честных PNG-скриншота дашборда (`shot-dashboard-projects/console/overview.png`) — единственные реальные "product visuals" в проекте, хорошего качества, перспективу/наклон можно дать чисто CSS (`perspective`/`rotateY`), без пересъёмки.
`public/fonts/`: полный набор Unbounded/Manrope/JetBrains Mono woff2 — ничего докачивать не нужно.

**Не хватает** (создать в Phase 2/3, см. `REDESIGN_PLAN.md` §9 asset-plan):
- Hero-визуал с игровой идентичностью GTA V (ключ-арт уровня "персонажи/City" — по правилам без копирования лицензированной графики Rockstar, оригинальная стилизация).
- 2–3 доп. атмосферных фото для zigzag/CTA-секций (уже подготовлена инфраструктура `OptionalPhoto`/`.ambient-photo`, файлы просто ещё не сгенерированы — см. память диалога, промпты уже выданы владельцу).

## 7. Локализация — состояние

RU/EN зеркалятся через `typeof ru` — структурно железно, дублирования ключей руками избежать сложно, но система работает без нареканий. Известный риск редизайна: любые новые тексты (hero, footer, language-block) нужно сразу добавлять в оба языковых блока `dict.ts`, иначе TS не соберётся (типы строгие).

## 8. Mobile — состояние

- Хедер: гамбургер + slide-sheet уже есть, работает.
- Hero (`engine-home__stage`) — `grid-template-columns: 150px minmax(0,1fr)` фиксированная левая "рельса" (`engine-rail`) на 150px — на мобильном это, вероятно, схлопывается через media-query (не проверено визуально в этом проходе — **зафиксировать как риск**, нужно визуально пройти на 375px до и после).
- Reveal/motion — не обёрнуто в `prefers-reduced-motion` проверку на уровне каждого нового мотива; общий `@media (prefers-reduced-motion: reduce)` блок в `globals.css` есть глобально (гасит transition/animation) — должно наследоваться автоматически, но новые перспективные 3D-трансформы (dashboard-tilt) нужно явно исключить из-под reduced-motion отдельно (наклон = не анимация per se, а статичный transform, значит не гасится текущим блоком — надо добавить правило).

## 9. Header/Footer — текущее полное состояние (для точки отсчёта)

**Header (`Navbar.tsx`)**: логотип+нейм слева → 5 nav-ссылок (Возможности/Лицензия/Проекты/Документация/Контакты) → справа: LangSwitch, логин-пилюля (или юзер+admin+logout), гамбургер-тема. Sticky не текущий (не `position:sticky`, обычный `relative` в потоке — проверить, возможно уже убрали sticky в одном из недавних коммитов `037cee8 align public header with production baseline`).

**Footer (`Footer.tsx`)**: карточка `rounded-2xl` на всю ширину контейнера → 4 колонки (Бренд+часы работы, Контакты, Документы, Продукт) → `rule-soft` → нижняя строка (копирайт, дескриптор, Telegram-ссылка). Языка нет.
