# План восстановления клиента alt:V

Обновлено: 2026-08-28
Вопрос владельца: «если зеркало alt:V не найдётся — всё в трубу?». **Нет.** Ниже почему.

---

## ИТОГ (2026-08-28, вечер): взят НАСТОЯЩИЙ клиент из GTAMP

Форумчанин прислал проект **GTAMP** (`docs/gtamp-reference.md`). В нём
`sources/payload/` — **бит-в-бит официальный клиент alt:V 16.4.39**
(sdk `c150769`, 86/86 hash-match с манифестом), та же версия что сервер.

`runtime/client/` пересобран из него: `scripts/import-altv-client.ps1
-PayloadDir "<gtamp>\sources\payload"`. **84/85 exact hash-match**,
единственный не-сток файл — `cef/altv-webengine.exe` (GTAMP модифицировал
под свой UI; для теста ок, для продакшена — заменить на сток).

Реконструкция ниже (CEF + Majestic) — больше не нужна, оставлена как история.
Все 4 «открытых вопроса» (freetype, icudtl_v8, legacy.dll, CEF-совместимость)
**сняты** — файлы настоящие.

---

## (история) РЕЗУЛЬТАТ реконструкции: клиент собран 85/85 файлов

`scripts/fetch-cef.ps1` + `scripts/assemble-client-core.ps1 -FillFrom` собрали
полный набор в `runtime/client/`:

- **17** файлов — из бэкапа alt:V (`altv.exe`, `altv-client.dll`,
  `altv-webengine.exe`, `libce2.dll`, `chrome_elf.dll` и т.д.);
- **62** файла — официальный CEF `131.3.5+chromium-131.0.6778.205` windows64
  minimal с `cef-builds.spotifycdn.com` (paks, V8-снапшоты, ANGLE/SwiftShader,
  `d3dcompiler_47`, `dxcompiler`/`dxil`, 55 локалей);
- **6** файлов — из локальной установки **Majestic RP** (`%APPDATA%\
  majestic-launcher\Multiplayer\libs\`; Majestic — RP-проект на форке alt:V):
  `legacy.dll` (99448 Б — **точное совпадение** с манифестом alt:V),
  `libcrypto-3-x64.dll`, `libssl-3-x64.dll`, `bassmix.dll`,
  `discord_game_sdk.dll` — все с точным совпадением размера;
  `freetype.dll` — 681984 Б vs ожидаемые 831608 (freetype 2.13.3, другой
  билд, ABI 2.x стабилен — **под вопросом**).
- `icudtl_v8.dat` — сделан копией `icudtl.dat` (в CEF такого файла нет) —
  **под вопросом**, проверить живым запуском.

### Что НЕ проверено (нужен владелец + GTA V)

1. Примет ли пропатченный alt:V `libce2.dll` (из бэкапа) ванильные
   CEF-ресурсы 131.0.6778.205. Версия Chromium та же — шанс высокий.
2. `freetype.dll` и `icudtl_v8.dat` (см. выше).
3. `altv.exe` при запуске может требовать валидацию по `update.json` —
   лаунчер передаёт `-noupdate`, должно пропускаться; если нет — инжектить
   `altv-client.dll` напрямую, минуя `altv.exe`.
4. `legacy.dll` из форка Majestic — размер совпал точно, скорее всего сток,
   но их форк мог тронуть.

**Живой тест: запустить `runtime/client/altv.exe` через лаунчер FloV:MP на
`127.0.0.1:7788`.** См. `docs/live-test-guide.md`.

### Идеальный вариант (по-прежнему полезен)

Полный официальный клиент alt:V 16.4.x от кого-то из RP-сообщества снимет
все 4 вопроса выше сразу. Собранное — рабочая гипотеза, не гарантия.

---

## Что известно точно

- Клиент в бэкапе: **17 из 85 файлов** (см. `docs/client-direct-connect.md`).
- Официальные источники alt:V мертвы: CDN — DNS нет; `altv.mp` — 502;
  GitHub `github.com/altmp` — жив, но там **только вспомогательные репы**
  (sentry-native, opus, natives, typings, docs…), **клиента/сервера нет** —
  `coreclr-module` и модули убраны из паблика после C&D Take-Two.
- Версия CEF/Chromium клиента: **131.0.6778.205** (из `chrome_elf.dll`,
  present). `altv-client.dll` версии 16.3.15. Лаунчер `altv.exe` — 14.4.

## Ключевой факт: почти все недостающие файлы — НЕ IP alt:V

Из 68 недостающих:

| Группа | Файлы | Источник (жив, не связан с alt:V) |
|---|---|---|
| **CEF 131.0.6778.205** | `resources.pak`, `chrome_100/200_percent.pak`, `icudtl.dat`, `snapshot_blob.bin`, `v8_context_snapshot.bin`, `vk_swiftshader_icd.json`, `d3dcompiler_47.dll`, все `cef/locales/*.pak` (~51) — итого ~58 | `cef-builds.spotifycdn.com` — **проверено 2026-08-28: билд `cef_binary_131.3.5+g573cec5+chromium-131.0.6778.205_windows64` есть** (minimal 190 МБ / standard 339 МБ). В архиве `Release/` + `Resources/`. |
| DirectX Shader Compiler | `dxcompiler.dll`, `dxil.dll` | `github.com/microsoft/DirectXShaderCompiler` releases (жив) |
| BASS mixer | `bassmix.dll` (bass.dll/bass_fx.dll уже есть) | `un4seen.com` (жив, бесплатно) |
| Discord GameSDK | `discord_game_sdk.dll` | discord.com/developers (бесплатно) |
| OpenSSL 3 | `libcrypto-3-x64.dll`, `libssl-3-x64.dll` | любой Win64-билд OpenSSL 3.x (slproweb / firedaemon / vcpkg) — ABI 3.x стабилен |
| прочее | `freetype.dll` | обычно в составе CEF-дистрибутива |
| **неясное** | `icudtl_v8.dat` | нестандартное имя. Возможно alt:V-переименование ICU-данных V8. Разобраться. |
| **IP alt:V, замены нет** | `legacy.dll` (~97 КБ) | шим-хук для **legacy** (до-Enhanced) `GTA5.exe`. Единственный по-настоящему проприетарный недостающий файл. |

Итог: **66 из 68 берутся из живых официальных источников третьих сторон.**
1 неясный. 1 проприетарный.

## `legacy.dll` — единственная реальная стена, и у неё есть обход

`legacy.dll` нужен потому, что у владельца **legacy** GTA V (Epic, 47 МБ,
до-Enhanced). Обход:

**Перейти на GTA V Enhanced.** Enhanced бесплатен для владельцев игры
(Steam обновляет автоматически; Epic даёт как отдельную позицию в библиотеке).
Для Enhanced клиент alt:V использует `altv-client.dll` (**он у нас есть**) и
`legacy.dll` **не требуется**.

Нужно подтвердить, что alt:V 16.4.39 поддерживает Enhanced (поддержка
добавлялась в 2025, была нестабильной). Если да — проприетарный блокер
исчезает полностью.

Если Enhanced не вариант — `legacy.dll` это **один файл 97 КБ**, найти его
у кого-то в разы проще, чем полный клиент.

## Порядок действий

1. **Спросить у RP-сообщества** (Discord alt:V-серверов, знакомые по
   Florida V/RAGE-сцене, archive.org, торренты). У alt:V были десятки тысяч
   пользователей — полный клиент лежит на тысячах машин и зеркалится после
   C&D. Подойдёт **любой полный 16.4.x**, или полный клиент+сервер более
   старой мажорной версии целиком. Это задача «поспрашивать пару дней», а
   не «невозможно».
2. **Если зеркала нет — реконструкция** (могу сделать я):
   a. скачать CEF `131.0.6778.205` windows64 (minimal) с cef-builds;
   b. вытащить `Release/*` и `Resources/*` в нужный layout;
   c. добрать `dxcompiler/dxil` (DXC), `bassmix` (un4seen),
      `discord_game_sdk` (Discord), OpenSSL 3 DLL;
   d. разобраться с `icudtl_v8.dat`;
   e. `legacy.dll`: либо перейти на Enhanced GTA V (не нужен), либо найти
      один файл.
   Затем `scripts/assemble-client-core.ps1 -FillFrom <собранное>` и
   живой тест.
   **Риск:** примет ли пропатченный `libce2.dll` alt:V ванильные CEF-ресурсы —
   проверяется только запуском. Шанс реальный (ресурсы привязаны к версии
   Chromium, а не к патчам alt:V).
3. **Если и это не выходит — план Б** (согласован с заказчиком): покупка
   готового решения. При этом сервер, лаунчер, отключение телеметрии,
   архитектура и вся разведка не пропадают — переносятся и на оценку/
   интеграцию купленного продукта.

## Вывод

«В трубу» — только при провале всех трёх уровней сразу: нет зеркала +
реконструкция не заводится + план Б отменён. Первые два независимы, третий
уже подстрахован. Ставим на п.1 (быстро), параллельно готовлю п.2.
