// Список команд у игрока: чат и консоль F8 должны показывать одно и то же —
// ровно то, что прислал сервер по правам этого игрока.
//
// Зачем. Раньше оба списка были зашиты в HTML и жили своей жизнью: консоль
// обещала команды, которых на сервере уже нет, и молчала о новых. Ошибка
// тихая — игрок просто видит «неизвестная команда» и считает сервер сломанным.
//
// Запуск:  node scripts/client-sim/commands_sync.test.mjs

import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
register(pathToFileURL(path.join(here, 'loader.mjs')).href, import.meta.url);

const alt = await import('alt-client');
const clientPath = path.resolve(here, '../../client/resources/flovmp-client/client/index.js');
await import(pathToFileURL(clientPath).href);

let failures = 0;
let passes = 0;

function check(ok, name, detail = '') {
    if (ok) { passes++; console.log(`  [PASS] ${name}`); }
    else { failures++; console.log(`  [FAIL] ${name}${detail ? ' — ' + detail : ''}`); }
}

function section(title) {
    console.log(`\n=== ${title} ===`);
}

const COMMANDS = JSON.stringify([
    { cmd: 'help', desc: 'список команд' },
    { cmd: 'kick', desc: 'исключить игрока' },
]);

function viewsBy(part) {
    return alt.__views().filter(v => v.url.includes(part));
}

function lastEmit(view, name) {
    const found = view.__emitted.filter(e => e[0] === name);
    return found.length ? found[found.length - 1] : null;
}

// ---------------------------------------------------------------------------
section('Список команд доходит и до чата, и до консоли');
alt.__reset();
alt.__fire('connectionComplete');
alt.__server('starter:initClient', 0, 0, 30);
alt.__advance(600);

const chat = viewsBy('chat')[0];
const console_ = viewsBy('console')[0];
check(!!chat && !!console_, 'чат и консоль созданы');

if (chat) chat.__fire('load');
if (console_) console_.__fire('load');

alt.__server('flovmp:chat:commands', COMMANDS);

check(!!chat && !!lastEmit(chat, 'flovmp:chat:commands'), 'чат получил список команд');
check(!!console_ && !!lastEmit(console_, 'flovmp:chat:commands'),
      'консоль получила тот же список',
      'иначе консоль показывает свой, зашитый в файл');

if (chat && console_) {
    const a = lastEmit(chat, 'flovmp:chat:commands')[1];
    const b = lastEmit(console_, 'flovmp:chat:commands')[1];
    check(a === b, 'списки совпадают дословно');
}

// ---------------------------------------------------------------------------
section('Консоль, открытая позже, тоже получает список');
alt.__reset();
alt.__fire('connectionComplete');
alt.__server('starter:initClient', 0, 0, 30);
alt.__advance(600);
alt.__server('flovmp:chat:commands', COMMANDS);

const consoleLate = viewsBy('console')[0];
check(!!consoleLate && !!lastEmit(consoleLate, 'flovmp:chat:commands'),
      'список не теряется, если он пришёл раньше открытия консоли');

console.log(`\n${'='.repeat(62)}`);
console.log(`Пройдено: ${passes}, провалов: ${failures}`);
process.exit(failures === 0 ? 0 : 1);
