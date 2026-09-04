// Скрипт визуальной проверки лаунчера через Playwright (_electron).
// Запуск: node test-visual.js
// Делает скриншоты всех страниц + состояний рельса/пикера в scratch-папку.
'use strict';
const { _electron: electron } = require('playwright');
const path = require('node:path');

const OUT = process.argv[2] || '.';

(async () => {
  const app = await electron.launch({ args: ['.'], cwd: __dirname });
  const win = await app.firstWindow();
  await win.waitForLoadState('domcontentloaded');
  await win.waitForTimeout(600);

  async function shot(name) {
    await win.screenshot({ path: path.join(OUT, `pw_${name}.png`) });
    console.log('shot:', name);
  }

  // 1. Играть, рельс свёрнут
  await shot('01-play-collapsed');

  // 2. Рельс развёрнут по наведению
  await win.hover('#rail');
  await win.waitForTimeout(250);
  await shot('02-play-rail-hover');

  // 3. Новости
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

  // 4. Настройки — верх
  await win.hover('#rail');
  await win.click('.rail-item[data-page="settings"]');
  await win.mouse.move(700, 500);
  await win.waitForTimeout(200);
  await shot('04-settings-top');

  // 5. Настройки — прокрутка вниз (весь список карточек)
  await win.evaluate(() => {
    document.querySelector('.settings-list').scrollTop = 999;
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
