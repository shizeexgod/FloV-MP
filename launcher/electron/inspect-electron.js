// Инспекция реального Electron (main.js + preload.js + нативный мост).
// Запуск: node inspect-electron.js
'use strict';
const { _electron: electron } = require('playwright');
const path = require('node:path');

(async () => {
  const app = await electron.launch({
    args: ['.'],
    cwd: __dirname,
    env: { ...process.env, FLOVMP_SPLASH_MIN: '0' },
  });
  let win = null;
  for (let i = 0; i < 120; i++) {
    win = app.windows().find((p) => p.url().includes('renderer/index.html'));
    if (win) break;
    await new Promise((r) => setTimeout(r, 100));
  }
  if (!win) throw new Error('нет главного окна');
  await win.waitForLoadState('domcontentloaded');
  await win.waitForTimeout(800);

  const errors = [];
  win.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  win.on('pageerror', (e) => errors.push(String(e)));

  // 1. Реальные аудиоустройства (Electron выдаёт с лейблами)
  const devices = await win.evaluate(async () => {
    try {
      const d = await navigator.mediaDevices.enumerateDevices();
      return d.map((x) => ({ kind: x.kind, label: x.label, id: x.deviceId.slice(0, 8) }));
    } catch (e) { return { error: String(e) }; }
  });
  console.log('\n=== enumerateDevices (реальный Electron) ===');
  console.log(JSON.stringify(devices, null, 2));

  // 2. Проверка что настройки реально сохраняются в файл
  await win.evaluate(() => window.floridaV.getSettings()).then((s) =>
    console.log('\n=== getSettings до изменений ===\n', JSON.stringify(s).slice(0, 400)));

  // меняем регион + масштаб через UI-логику и сохраняем
  const saved = await win.evaluate(async () => {
    settings.region = 'eu';
    settings.uiScale = 115;
    settings.graphicsPreset = 'high';
    await window.floridaV.saveSettings(settings);
    const back = await window.floridaV.getSettings();
    return { region: back.region, uiScale: back.uiScale, graphicsPreset: back.graphicsPreset };
  });
  console.log('\n=== после saveSettings → getSettings (тот же процесс) ===\n', JSON.stringify(saved));

  // 3. Новостная обложка — реальные размеры vs карточка (проверка «до краёв»)
  await win.click('.rail-item[data-page="news"]');
  await win.waitForTimeout(400);
  const cover = await win.evaluate(() => {
    const card = document.querySelector('.news-full-card');
    const thumb = card && card.querySelector('.news-thumb');
    if (!card || !thumb) return 'нет карточки';
    const c = card.getBoundingClientRect();
    const t = thumb.getBoundingClientRect();
    const cs = getComputedStyle(card);
    return {
      cardW: Math.round(c.width), thumbW: Math.round(t.width),
      leftGap: Math.round(t.left - c.left), rightGap: Math.round(c.right - t.right),
      topGap: Math.round(t.top - c.top),
      cardPadding: cs.padding, cardBorder: cs.borderWidth,
      thumbClass: thumb.className,
      hasImage: thumb.style.backgroundImage || '(нет)',
    };
  });
  console.log('\n=== обложка новости vs карточка ===\n', JSON.stringify(cover, null, 2));

  // 4. Голос — реальные устройства в кастомном селекте + сохранение выбора
  await win.click('#btn-open-settings');
  await win.waitForTimeout(300);
  await win.click('.settings-subnav [data-subtab="voice"]');
  await win.waitForTimeout(500);
  const voice = await win.evaluate(async () => {
    const inSel = document.getElementById('set-voice-input');
    const xs = inSel && inSel._xs;
    const opts = [...inSel.options].map((o) => o.textContent);
    const menuOpts = xs ? [...xs.menu.querySelectorAll('.xselect__opt')].map((r) => r.textContent) : [];
    // выбрать второе устройство через кастомное меню
    let picked = null;
    if (xs && menuOpts.length > 1) {
      xs.menu.querySelectorAll('.xselect__opt')[1].click();
      await new Promise((r) => setTimeout(r, 100));
      picked = { settingVal: settings.voiceInput, selVal: inSel.value, label: xs.btn.querySelector('.xselect__label').textContent };
    }
    await window.floridaV.saveSettings(settings);
    const back = await window.floridaV.getSettings();
    return { nativeOpts: opts, menuOpts, picked, persisted: back.voiceInput };
  });
  console.log('\n=== голос: реальные устройства + сохранение ===\n', JSON.stringify(voice, null, 2));

  // 5. Анимация закрытия кастомного селекта
  await win.click('.settings-subnav [data-subtab="game"]');
  await win.waitForTimeout(200);
  const anim = await win.evaluate(async () => {
    const xs = document.querySelector('#page-settings .xselect, .settings-tab.active .xselect');
    if (!xs) return 'нет селекта';
    xs.querySelector('.xselect__btn').click();
    await new Promise((r) => setTimeout(r, 250));
    const openState = { open: xs.classList.contains('open'), menuOpacity: getComputedStyle(xs.querySelector('.xselect__menu')).opacity };
    xs.querySelector('.xselect__btn').click();
    await new Promise((r) => setTimeout(r, 80));
    const midClose = getComputedStyle(xs.querySelector('.xselect__menu')).opacity;
    await new Promise((r) => setTimeout(r, 250));
    const doneClose = { opacity: getComputedStyle(xs.querySelector('.xselect__menu')).opacity, vis: getComputedStyle(xs.querySelector('.xselect__menu')).visibility };
    return { openState, midClose, doneClose };
  });
  console.log('\n=== анимация закрытия селекта ===\n', JSON.stringify(anim, null, 2));

  await win.screenshot({ path: path.join(__dirname, 'tests', 'screenshots', 'el_news.png') });
  await win.click('.rail-item[data-page="news"]').catch(() => {});
  await win.waitForTimeout(200);
  await win.screenshot({ path: path.join(__dirname, 'tests', 'screenshots', 'el_news2.png') });

  console.log('\n=== console errors ===', JSON.stringify(errors));
  await app.close();
})();
