// Симуляция процесса захода в игру для клиентского скрипта FloV:MP.
//
// Зачем. Клиентский скрипт выполняется внутри GTA V, и проверить его «на
// живую» можно только полноценным запуском игры. Но самые дорогие ошибки
// входа — логические: экран, который не закрывается; чат, который теряет
// сообщения; курсор, который остаётся висеть. Они проверяются без игры.
//
// Здесь подменяются модули `alt-client` и `natives`, реальный index.js
// загружается как есть, и по нему прогоняются сценарии обоих режимов сервера.
//
// Запуск:  node scripts/client-sim/entry_flow.test.mjs

import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
register(pathToFileURL(path.join(here, 'loader.mjs')).href, import.meta.url);

const alt = await import('alt-client');
const client = path.resolve(here, '../../client/resources/flovmp-client/client/index.js');
await import(pathToFileURL(client).href);

let failures = 0;
let passes = 0;

function check(ok, name, detail = '') {
    if (ok) { passes++; console.log(`  [PASS] ${name}`); }
    else { failures++; console.log(`  [FAIL] ${name}${detail ? ' — ' + detail : ''}`); }
}

function section(title) {
    console.log(`\n=== ${title} ===`);
}

// ---------------------------------------------------------------------------
section('Базовая платформа (flovmp-starter): входа нет, спавн сразу');
alt.__reset();

alt.__fire('connectionComplete');
check(alt.__views().some(v => v.url.includes('loading')), 'загрузочный экран открыт при подключении');
check(!alt.__views().some(v => v.url.includes('chat')), 'чат НЕ открыт поверх загрузки');

// Сервер пишет в чат, пока идёт загрузка (подсказка владельцу).
alt.__server('flovmp:chat:msg', 'system', '', 'токен настройки: FLV-TEST');

alt.__server('starter:initClient', 100, 200, 30);
alt.__advance(600); // спавн: коллизия «загружается» за несколько итераций

check(!alt.__views().some(v => v.url.includes('loading')),
      'загрузочный экран закрыт после спавна',
      'в базовой платформе нет формы входа — закрыть иначе было бы нечем');

const chat = alt.__views().find(v => v.url.includes('chat'));
check(!!chat, 'чат открыт после загрузки');

if (chat) {
    chat.__fire('load');
    check(chat.__emitted.some(e => e[0] === 'flovmp:chat:msg' && String(e[3]).includes('FLV-TEST')),
          'сообщение, пришедшее во время загрузки, доставлено в чат',
          'без буфера подсказка с токеном потерялась бы молча');
}

check(alt.__controls() === true, 'управление игрой возвращено');
check(alt.__cursor() === false, 'курсор не висит на экране');

// ---------------------------------------------------------------------------
section('Страховка: сервер не прислал ни одного события');
alt.__reset();

alt.__fire('connectionComplete');
check(alt.__views().some(v => v.url.includes('loading')), 'загрузочный экран открыт');

alt.__advance(29000);
check(alt.__views().some(v => v.url.includes('loading')), 'через 29 с ещё открыт (ждём честно)');

alt.__advance(2000);
check(!alt.__views().some(v => v.url.includes('loading')),
      'через 31 с снят принудительно',
      'экран не имеет права запереть игрока навсегда');
check(alt.__controls() === true, 'управление возвращено после таймаута');

// ---------------------------------------------------------------------------
section('Отключение во время загрузки');
alt.__reset();

alt.__fire('connectionComplete');
alt.__fire('disconnect');

check(!alt.__views().some(v => v.url.includes('loading')), 'загрузочный экран снят при отключении');
check(alt.__cursor() === false, 'курсор не остался после отключения');
check(alt.__controls() === true, 'управление возвращено после отключения');

console.log(`\n${'='.repeat(62)}`);
console.log(`Пройдено: ${passes}, провалов: ${failures}`);
process.exit(failures > 0 ? 1 : 0);
