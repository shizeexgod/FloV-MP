// Скрипт визуальной проверки лаунчера через Playwright (_electron).
// Запуск: node test-visual.js
// Делает скриншоты всех страниц + состояний рельса/пикера в scratch-папку.
'use strict';
const { _electron: electron } = require('playwright');
const path = require('node:path');

const OUT = process.argv[2] || '.';

(async () => {
  const app = await electron.launch({ args: ['.'], cwd: __dirname });

  // Теперь при старте сначала появляется отдельное окно-заставка (splash.html),
  // потом главное (index.html) — firstWindow() может поймать заставку.
  // Ждём именно окно с index.html, опрашивая app.windows().
  let win = null;
  for (let i = 0; i < 50; i++) {
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

  // 1. Играть (рельс статичный)
  await shot('01-play');

  // 2. Ресурсы — флайаут по одной иконке
  await win.click('#btn-resources');
  await win.waitForTimeout(250);
  await shot('02-resources-flyout');
  await win.keyboard.press('Escape');
  await win.waitForTimeout(150);

  // 3. Новости — переход по кнопке «Все» в блоке новостей рельса
  await win.click('.rail-news-all');
  await win.mouse.move(700, 500);
  await win.waitForTimeout(200);
  await shot('03-news');

  // 3b. Открытие модального окна новости
  await win.click('.news-full-card[data-news-id="1"]');
  await win.waitForTimeout(300);
  await shot('03b-news-modal');
  await win.click('#news-modal-close');
  await win.waitForTimeout(200);

  // 4. Настройки — всплывающее окно поверх текущего экрана (не страница)
  await win.hover('#rail');
  await win.click('#btn-open-settings');
  await win.mouse.move(700, 500);
  await win.waitForTimeout(400);
  await shot('04-settings-modal-open');

  // 4b. Вкладка «Дополнительно» внутри модалки
  await win.click('.settings-subnav [data-subtab="extra"]');
  await win.waitForTimeout(200);
  await shot('04b-settings-extra');
  await win.click('.settings-subnav [data-subtab="main"]');
  await win.waitForTimeout(150);

  // 5. Настройки — прокрутка вниз (активная вкладка "Основное")
  await win.evaluate(() => {
    document.querySelector('.settings-tab.active .settings-list').scrollTop = 999;
  });
  await win.waitForTimeout(150);
  await shot('05-settings-bottom');

  // 6. Клик по акцентному пресету (blue) — живая перекраска
  const blueSwatch = await win.$('.accent-swatch[data-accent="blue"]');
  if (blueSwatch) {
    await blueSwatch.click();
    await win.waitForTimeout(200);
    await shot('06-settings-blue-accent');
    // вернуть на золотой
    const goldSwatch = await win.$('.accent-swatch[data-accent="gold"]');
    if (goldSwatch) await goldSwatch.click();
  }

  // 6b. Закрыть настройки — под ними должна остаться страница как была (без блюра)
  await win.click('#settings-close');
  await win.waitForTimeout(300);
  await shot('06b-settings-closed');

  // 7. Консоль на ошибки
  const errors = [];
  win.on('console', (msg) => { if (msg.type() === 'error') errors.push(msg.text()); });
  await win.waitForTimeout(300);
  console.log('console errors:', JSON.stringify(errors));

  await app.close();
})().catch((err) => {
  console.error('TEST FAILED:', err);
  process.exit(1);
});
