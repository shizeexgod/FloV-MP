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

  // 6. Звуки интерфейса — файлы реально грузятся?
  const snd = await win.evaluate(async () => {
    const names = ['hover', 'click', 'select', 'modal', 'modalOut', 'toggle', 'error'];
    const out = {};
    for (const n of names) {
      try {
        const r = await fetch(`assets/sounds/${n}.ogg`);
        out[n] = r.ok ? `${(await r.arrayBuffer()).byteLength}b` : `HTTP ${r.status}`;
      } catch (e) { out[n] = 'FAIL'; }
    }
    return out;
  });
  console.log('\n=== звуки интерфейса (fetch) ===\n', JSON.stringify(snd, null, 2));

  // 7. Свой цвет — HSV-пикер, не нативный <input type=color>
  await win.click('.settings-subnav [data-subtab="interface"]');
  await win.waitForTimeout(200);
  const picker = await win.evaluate(() => {
    const custom = document.querySelector('.accent-swatch--custom, [data-accent="custom"]');
    if (!custom) return 'нет кнопки своего цвета';
    custom.click();
    const pop = document.querySelector('.hsv-pop, .color-pop, .accent-custom-pop, input[type="color"]');
    return {
      opened: !!pop,
      isNativeInput: pop ? pop.tagName === 'INPUT' && pop.type === 'color' : null,
      popClass: pop ? pop.className : null,
    };
  });
  console.log('\n=== пикер своего цвета ===\n', JSON.stringify(picker, null, 2));

  const shots = [
    ['el_settings_iface', async () => { await win.click('.settings-subnav [data-subtab="interface"]'); await win.waitForTimeout(300); }],
    ['el_settings_server', async () => { await win.click('.settings-subnav [data-subtab="general"]'); await win.waitForTimeout(300); }],
    ['el_play', async () => { await win.keyboard.press('Escape'); await win.waitForTimeout(300); await win.click('.rail-item[data-page="play"]'); await win.waitForTimeout(300); }],
  ];
  for (const [name, act] of shots) {
    try { await act(); await win.screenshot({ path: path.join(__dirname, 'tests', 'screenshots', name + '.png') }); }
    catch (e) { console.log('shot', name, 'fail', e.message); }
  }

  console.log('\n=== console errors ===', JSON.stringify(errors));
  await app.close();
})();
