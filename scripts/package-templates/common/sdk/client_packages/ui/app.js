// Страница сервера. window.mp даёт платформа, как в RAGE:MP:
//   mp.events.add(имя, fn)  — событие от клиентского кода (browser.call);
//   mp.trigger(имя, …)      — событие в клиентский код (mp.events.add в index.js).

const money = document.getElementById('money');
const player = document.getElementById('player');
const panel = document.getElementById('panel');
const form = document.getElementById('report');
const input = document.getElementById('report-text');
const formatMoney = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });

mp.events.add('hud:money', (value) => { money.textContent = formatMoney.format(value); });
mp.events.add('hud:player', (name) => { player.textContent = name; });

mp.events.add('panel:toggle', (open) => {
  panel.hidden = !open;
  if (open) { input.value = ''; input.focus(); }
});

document.getElementById('close').addEventListener('click', () => mp.trigger('panel:close'));

form.addEventListener('submit', (e) => {
  e.preventDefault();
  const text = input.value.trim();
  if (!text) { input.focus(); return; }
  mp.trigger('panel:submit', text);
});

document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && !panel.hidden) mp.trigger('panel:close');
});
