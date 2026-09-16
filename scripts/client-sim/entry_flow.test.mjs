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
section('RP-режим (flovmp-core): форма входа, затем спавн');
alt.__reset();

alt.__fire('connectionComplete');
check(alt.__views().some(v => v.url.includes('loading')), 'загрузочный экран открыт');

alt.__server('flovmp:auth:show', 'Держава RP');
check(!alt.__views().some(v => v.url.includes('loading')), 'загрузка закрыта, когда пришла форма входа');

const auth = alt.__views().find(v => v.url.includes('auth'));
check(!!auth, 'форма входа открыта');
check(alt.__cursor() === true, 'курсор показан для ввода пароля');
check(alt.__controls() === false, 'управление заблокировано, пока вводится пароль',
      'иначе персонаж бегал бы, пока игрок печатает');

if (auth) {
    auth.__fire('flovmp:auth:login', 'Ivan_Petrov', 'secret123');
    check(alt.__toServer().some(e => e[0] === 'flovmp:auth:login' && e[1] === 'Ivan_Petrov'),
          'логин уходит на сервер');

    alt.__server('flovmp:auth:result', false, 'неверный пароль');
    check(auth.__emitted.some(e => e[0] === 'flovmp:auth:result' && e[1] === false),
          'ошибка входа доходит до формы');
}

alt.__server('flovmp:auth:hide');
check(!alt.__views().some(v => v.url.includes('auth')), 'форма входа закрыта после успешного входа');
check(alt.__cursor() === false, 'курсор убран после входа');
check(alt.__controls() === true, 'управление возвращено после входа');

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
section('Отключение посреди входа');
alt.__reset();

alt.__fire('connectionComplete');
alt.__server('flovmp:auth:show', 'Сервер');
alt.__fire('disconnect');

check(alt.__views().length === 0 || !alt.__views().some(v => v.url.includes('auth') || v.url.includes('loading')),
      'экраны входа и загрузки сняты при отключении');
check(alt.__cursor() === false, 'курсор не остался после отключения');

// ---------------------------------------------------------------------------
section('Повторная команда показа входа (страховочная отправка сервера)');
alt.__reset();

alt.__fire('connectionComplete');
alt.__server('flovmp:auth:show', 'Сервер');
alt.__server('flovmp:auth:show', 'Сервер');

const authCount = alt.__views().filter(v => v.url.includes('auth')).length;
check(authCount === 1, 'форма входа не открывается второй раз', `окон входа: ${authCount}`);
check(alt.__cursorDepthSane(), 'счётчик курсора не ушёл в плюс от дубля');

// ---------------------------------------------------------------------------
section('HUD (полный RP-режим): кадры сервера доходят до экрана');
alt.__reset();

alt.__fire('connectionComplete');
alt.__server('flovmp:auth:show', 'Держава RP');
alt.__server('flovmp:auth:hide');

// Сервер шлёт init и первый кадр, пока страница HUD ещё грузится.
alt.__server('flovmp:hud:init', 'Держава RP');
alt.__server('flovmp:hud:tick', 80, 50, 125000, 342, 14, 5);

const hud = alt.__views().find(v => v.url.includes('hud'));
check(!!hud, 'HUD открыт по команде сервера',
      'раньше клиент не слушал flovmp:hud:init — здоровья и денег на экране не было');

if (hud) {
    check(!hud.__emitted.some(e => e[0] === 'flovmp:hud:tick'),
          'до загрузки страницы кадр не отправляется в пустоту');

    hud.__fire('load');
    check(hud.__emitted.some(e => e[0] === 'flovmp:hud:init' && e[1] === 'Держава RP'),
          'init, пришедший до загрузки страницы, доставлен');
    check(hud.__emitted.some(e => e[0] === 'flovmp:hud:tick' && e[3] === 125000),
          'последний кадр, пришедший до загрузки, доставлен сразу',
          'иначе игрок видел бы нули до следующего кадра сервера');

    alt.__server('flovmp:hud:tick', 60, 50, 124000, 342, 14, 6);
    check(hud.__emitted.some(e => e[0] === 'flovmp:hud:tick' && e[3] === 124000),
          'новые кадры доходят после загрузки');

    check(hud.focused === false, 'HUD не забирает фокус',
          'иначе перехватил бы клавиатуру у игрока');
}

check(alt.__controls() === true, 'HUD не блокирует управление');
check(alt.__cursor() === false, 'HUD не показывает курсор');

alt.__server('flovmp:inv:notice', 'Вы скованы наручниками');
if (hud) {
    check(hud.__emitted.some(e => e[0] === 'flovmp:hud:notice' && String(e[1]).includes('наручник')),
          'отказ инвентаря показывается игроку',
          'раньше событие игнорировалось — игрок не понимал, почему предмет не двигается');
}

alt.__fire('disconnect');
check(!alt.__views().some(v => v.url.includes('hud')), 'HUD снят при отключении');

// ---------------------------------------------------------------------------
section('Уведомление без HUD (базовая платформа) уходит в чат');
alt.__reset();

alt.__fire('connectionComplete');
alt.__server('starter:initClient', 0, 0, 30);
alt.__advance(600);
const chat2 = alt.__views().find(v => v.url.includes('chat'));
if (chat2) chat2.__fire('load');

alt.__server('flovmp:inv:notice', 'Инвентарь полон');
check(!!chat2 && chat2.__emitted.some(e => e[0] === 'flovmp:chat:msg' && String(e[3]).includes('полон')),
      'уведомление не пропадает, когда HUD нет');

console.log(`\n${'='.repeat(62)}`);
console.log(`Пройдено: ${passes}, провалов: ${failures}`);
process.exit(failures > 0 ? 1 : 0);
