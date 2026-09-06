// Скрипт визуальной проверки лаунчера через Playwright (_electron).
// Запуск: node test-visual.js
// Делает скриншоты всех страниц + состояний рельса/пикера в scratch-папку.
'use strict';
const { _electron: electron } = require('playwright');
const path = require('node:path');

const OUT = process.argv[2] || '.';

(async () => {
  const app = await electron.launch({
    args: ['.'],
    cwd: __dirname,
    env: { ...process.env, FLOVMP_SPLASH_MIN: '0' },
  });

  // Теперь при старте сначала появляется отдельное окно-заставка (splash.html),
  // потом главное (index.html) — firstWindow() может поймать заставку.
  // Ждём именно окно с index.html, опрашивая app.windows().
  let win = null;
  for (let i = 0; i < 120; i++) {
    const pages = app.windows();
    win = pages.find((p) => p.url().includes('renderer/index.html'));
    if (win) break;
    await new Promise((r) => setTimeout(r, 100));
  }
  if (!win) throw new Error('Не дождался главного окна (index.html) — осталась только заставка?');
  await win.waitForLoadState('domcontentloaded');
  await win.waitForTimeout(600);

  async function shot(name) {
    await win.screenshot({ path: path.join(OUT, `pw_${name}.png`) });
    console.log('shot:', name);
  }

  // Слушаем консоль с самого начала, а не в конце — иначе ранние ошибки
  // (инициализация, первые клики) не попадают в отчёт.
  const errors = [];
  win.on('console', (msg) => { if (msg.type() === 'error') errors.push(msg.text()); });
  win.on('pageerror', (e) => errors.push(String(e)));

  // 0. Стартуем гостем: сбрасываем аккаунт (в settings.json мог остаться от
  //    прошлого прогона) — сценарий проверяет путь «гость → вход».
  await win.evaluate(async () => {
    settings.account = null;
    applyAccountUI();
    await window.floridaV.saveSettings(settings);
  });
  await win.waitForTimeout(200);
  // Модалка входа больше не всплывает сама — но на всякий случай закрываем.
  const authOv = win.locator('#auth-overlay:not(.hidden)');
  if (await authOv.count()) {
    await win.click('#auth-skip');
    await win.waitForTimeout(300);
  }

  // 1. Играть (рельс статичный)
  await win.waitForSelector('#status-dot.online', { timeout: 4000 }).catch(() => null);
  await shot('01-play');

  // 2. Ресурсы — флайаут по одной иконке
  await win.click('#btn-resources');
  await win.waitForTimeout(250);
  await shot('02-resources-flyout');
  await win.keyboard.press('Escape');
  await win.waitForTimeout(150);

  // 3. Новости — иконка рельса
  await win.click('.rail-item[data-page="news"]');
  await win.mouse.move(700, 500);
  await win.waitForTimeout(200);
  await shot('03-news');

  // 3b. Открытие модального окна новости
  await win.click('.news-full-card[data-news-id="1"]');
  await win.waitForTimeout(300);
  await shot('03b-news-modal');
  await win.click('#news-modal-close');
  await win.waitForTimeout(200);

  // 3c. Профиль → окно входа/регистрации (гость). Регистрируемся, ник в
  //     рельсе должен смениться с «Игрок» на логин.
  await win.click('#btn-open-cabinet');
  await win.waitForTimeout(300);
  await shot('03c-auth-modal');
  await win.click('#btn-toggle-mode'); // режим регистрации
  await win.fill('#auth-login', 'pwtester');
  await win.fill('#auth-password', 'pwtest123');
  await win.click('#btn-auth-submit');
  await win.waitForTimeout(600);
  let nickAfter = await win.textContent('#account-nick').catch(() => '');
  if (!/pwtester/i.test(nickAfter || '')) {
    // Сервера авторизации в тесте нет — форсируем вход, чтобы снять кабинет.
    console.log('note: ServerLauncher не запущен, форсирую вход для скриншотов кабинета');
    await win.evaluate(() => {
      settings.account = { username: 'pwtester', createdUtc: new Date().toISOString(), email: '', twoFa: false };
      applyAccountUI();
    });
    await win.click('#auth-skip').catch(() => {});
    await win.waitForTimeout(200);
    nickAfter = await win.textContent('#account-nick').catch(() => '');
  }
  if (!/pwtester/i.test(nickAfter || '')) throw new Error('ник не сменился после входа: ' + JSON.stringify(nickAfter));

  // 3d. Личный кабинет — 4 вкладки
  await win.click('#btn-open-cabinet');
  await win.waitForTimeout(300);
  await shot('03d-cabinet-profile');
  for (const tab of ['security', 'devices', 'history']) {
    await win.click(`.cabinet-item[data-tab="${tab}"]`);
    await win.waitForTimeout(250);
    await shot(`03d-cabinet-${tab}`);
  }
  await win.click('#cabinet-close');
  await win.waitForTimeout(250);

  // 4. Настройки — всплывающее окно поверх текущего экрана (не страница)
  await win.hover('#rail');
  await win.click('#btn-open-settings');
  await win.mouse.move(700, 500);
  await win.waitForTimeout(400);
  await shot('04-settings-modal-open');

  // 4b. Вкладка «Игра» внутри модалки
  await win.click('.settings-subnav [data-subtab="game"]');
  await win.waitForTimeout(200);
  await shot('04b-settings-game');
  await win.click('.settings-subnav [data-subtab="voice"]');
  await win.waitForTimeout(200);
  await shot('04c-settings-voice');
  await win.click('.settings-subnav [data-subtab="general"]');
  await win.waitForTimeout(150);

  // 5. Настройки — прокрутка вниз (активная вкладка "Основное")
  await win.evaluate(() => {
    document.querySelector('.settings-tab.active .settings-list').scrollTop = 999;
  });
  await win.waitForTimeout(150);
  await shot('05-settings-bottom');

  // 6. Клик по акцентному пресету (blue) — живая перекраска.
  //    Пикер акцента живёт во вкладке «Интерфейс».
  await win.click('.settings-subnav [data-subtab="interface"]');
  await win.waitForTimeout(200);
  const blueSwatch = await win.$('.accent-swatch[data-accent="blue"]');
  if (blueSwatch) {
    await blueSwatch.click();
    await win.waitForTimeout(200);
    await shot('06-settings-blue-accent');
    // вернуть на золотой
    const goldSwatch = await win.$('.accent-swatch[data-accent="gold"]');
    if (goldSwatch) await goldSwatch.click();
  }

  // 6b. Закрыть настройки — под ними должна остаться страница как была (без блюра).
  //     Проверяем именно ПЛАВНОЕ закрытие: сразу после клика окно ещё видно
  //     (идёт анимация), после паузы — полностью скрыто (opacity:0, не reflow).
  await win.click('#settings-close');
  const midClose = await win.evaluate(() => {
    const o = document.getElementById('settings-overlay');
    return { hasHidden: o.classList.contains('hidden'), opacity: getComputedStyle(o).opacity };
  });
  if (!midClose.hasHidden) throw new Error('settings-close не повесил .hidden');
  if (midClose.opacity === '0') console.log('WARN: настройки закрылись мгновенно (opacity 0 сразу) — нет анимации');
  await win.waitForTimeout(400);
  const doneClose = await win.evaluate(() => {
    const o = document.getElementById('settings-overlay');
    const cs = getComputedStyle(o);
    return { opacity: cs.opacity, visibility: cs.visibility, pe: cs.pointerEvents };
  });
  if (doneClose.opacity !== '0' || doneClose.visibility !== 'hidden') {
    throw new Error('настройки не скрылись после анимации: ' + JSON.stringify(doneClose));
  }
  await shot('06b-settings-closed');

  // 7. Консоль на ошибки (слушатель повешен в начале)
  await win.waitForTimeout(300);
  console.log('console errors:', JSON.stringify(errors));

  await app.close();
  if (errors.length) process.exit(1);
})().catch((err) => {
  console.error('TEST FAILED:', err);
  process.exit(1);
});
