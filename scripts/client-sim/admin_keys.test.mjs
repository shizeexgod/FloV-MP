// Клавиши администратора в клиенте: F3 (ESP), F4 (NoClip), F5 (телепорт).
//
// Зачем эта проверка. Уровень каждой команды задаёт владелец сервера в
// server/config/admin-commands.cfg. Если клиент решает «можно ли», глядя на
// «уровень больше нуля», он расходится с сервером: владелец поднял noclip до
// восьмого уровня, а F4 у младшего администратора всё равно срабатывает.
// Клиент обязан спрашивать тот же список команд, что сервер ему прислал.
//
// Запуск:  node scripts/client-sim/admin_keys.test.mjs

import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
register(pathToFileURL(path.join(here, 'loader.mjs')).href, import.meta.url);

const alt = await import('alt-client');
const clientPath = path.resolve(here, '../../client/resources/flovmp-client/client/index.js');
const client = await import(pathToFileURL(clientPath).href);

let failures = 0;
let passes = 0;

function check(ok, name, detail = '') {
    if (ok) { passes++; console.log(`  [PASS] ${name}`); }
    else { failures++; console.log(`  [FAIL] ${name}${detail ? ' — ' + detail : ''}`); }
}

function section(title) {
    console.log(`\n=== ${title} ===`);
}

/** Включился ли NoClip: в нём клиент замораживает игрока и снимает столкновения. */
function noClipOn() {
    return client.isNoClipActive ? client.isNoClipActive() : null;
}

// ---------------------------------------------------------------------------
section('Обычный игрок: клавиши администратора не работают');
alt.__reset();
alt.__fire('connectionComplete');
alt.__server('starter:initClient', 0, 0, 30);
alt.__advance(600);
alt.__server('flovmp:console:setAdmin', 0);
alt.__server('flovmp:chat:commands', JSON.stringify([{ cmd: 'help', desc: 'команды' }]));

client.toggleNoClip();
check(noClipOn() === false, 'NoClip не включается без прав');
client.toggleEsp();
check(true, 'ESP без прав не падает');

// ---------------------------------------------------------------------------
section('Администратор: работает ровно то, что разрешил сервер');
alt.__server('flovmp:console:setAdmin', 8);
alt.__server('flovmp:chat:commands', JSON.stringify([
    { cmd: 'help', desc: 'команды' },
    { cmd: 'noclip', desc: 'полёт' },
]));

client.toggleNoClip();
check(noClipOn() === true, 'NoClip включается, когда сервер прислал команду noclip');
client.toggleNoClip();
check(noClipOn() === false, 'повторное нажатие выключает NoClip');

// ---------------------------------------------------------------------------
section('Команда убрана из прав: клавиша перестаёт работать');
alt.__server('flovmp:chat:commands', JSON.stringify([{ cmd: 'help', desc: 'команды' }]));
client.toggleNoClip();
check(noClipOn() === false, 'NoClip не включается, хотя уровень администратора остался 8',
      'иначе клиент расходится с server/config/admin-commands.cfg');

// ---------------------------------------------------------------------------
section('Права отняли посреди полёта: NoClip выключается сам');
alt.__server('flovmp:chat:commands', JSON.stringify([
    { cmd: 'help', desc: 'команды' },
    { cmd: 'noclip', desc: 'полёт' },
]));
client.toggleNoClip();
check(noClipOn() === true, 'полёт включён, пока право есть');

alt.__server('flovmp:chat:commands', JSON.stringify([{ cmd: 'help', desc: 'команды' }]));
check(noClipOn() === false, 'после снятия права полёт выключен без нажатия клавиши',
      'иначе снятый администратор летал бы до перезахода');

console.log(`\n${'='.repeat(62)}`);
console.log(`Пройдено: ${passes}, провалов: ${failures}`);
process.exit(failures === 0 ? 0 : 1);
