// Первый серверный срез RAGE:MP поверх событий FloV:MP.
// Входной bridge = alt-server; инъекция позволяет тестировать без игрового движка.
const EVENT_NAME = /^[A-Za-z0-9_:.-]{1,64}$/;
const VARIABLE_NAME = /^[A-Za-z0-9_:.-]{1,64}$/;
const MAX_LINE_BYTES = 4096;
const MAX_JSON_CHARS = 3800;

function eventName(name) {
  if (typeof name !== 'string' || !EVENT_NAME.test(name)) {
    throw new TypeError('имя события: 1–64 символа [A-Za-z0-9_:.-]');
  }
  return name;
}

function checkWireSize(prefix, json) {
  // NativeProtocol.Format экранирует каждый обратный слеш ещё раз.
  if (json.length > MAX_JSON_CHARS ||
      Buffer.byteLength(prefix + json.replace(/\\/g, '\\\\'), 'utf8') > MAX_LINE_BYTES) {
    throw new RangeError('аргументы превышают лимит FLOV/2');
  }
}

function jsonArgs(name, args) {
  const json = JSON.stringify(args);
  checkWireSize(`CEV\t${name}\t`, json);
  return json;
}

export function createRageCompat(alt) {
  const players = new Map();
  const handlers = new Map();
  const commands = new Map();

  function dispatch(name, ...args) {
    for (const fn of [...(handlers.get(name) || [])]) {
      try { fn(...args); }
      catch (error) { alt.logError?.(`[RAGE compat] ${name}: ${error?.stack || error}`); }
    }
  }

  function add(name, fn) {
    if (name && typeof name === 'object' && !Array.isArray(name)) {
      for (const [key, handler] of Object.entries(name)) add(key, handler);
      return;
    }
    eventName(name);
    if (typeof fn !== 'function') throw new TypeError('обработчик должен быть функцией');
    if (!handlers.has(name)) handlers.set(name, new Set());
    handlers.get(name).add(fn);
  }

  function remove(name, fn) {
    if (Array.isArray(name)) { for (const key of name) remove(key, fn); return; }
    if (fn === undefined) handlers.delete(name);
    else handlers.get(name)?.delete(fn);
  }

  class Player {
    constructor(id, name) {
      this.id = id;
      this.remoteId = id;
      this.name = name;
      this.type = 'player';
      this._variables = new Map();
    }

    call(name, args = []) {
      eventName(name);
      const values = Array.isArray(args) ? args : [args];
      alt.emit('flovmp:client:call', this.id, name, jsonArgs(name, values));
    }

    setVariable(key, value) {
      if (typeof key !== 'string' || !VARIABLE_NAME.test(key)) throw new TypeError('неверный ключ переменной');
      const json = JSON.stringify(value);
      if (json === undefined) {
        throw new RangeError('недопустимое значение переменной');
      }
      checkWireSize(`SVAR\t${this.id}\t${key}\t`, json);
      if (value === null) this._variables.delete(key);
      else this._variables.set(key, value);
      alt.emit('flovmp:player:setVariable', this.id, key, json);
    }

    getVariable(key) { return this._variables.get(key); }
    hasVariable(key) { return this._variables.has(key); }
  }

  const mp = {
    events: {
      add, remove, reset: () => handlers.clear(),
      call: (name, ...args) => dispatch(eventName(name), ...args),
      getAllOf: (name) => [...(handlers.get(name) || [])],
      addCommand(name, fn) {
        eventName(name);
        if (typeof fn !== 'function') throw new TypeError('обработчик команды должен быть функцией');
        const command = name.toLowerCase();
        commands.set(command, fn);
        alt.emit('flovmp:commands:register', command, `RAGE:MP /${command}`, 0);
      },
    },
    players: {
      at: (id) => players.get(Number(id)) || null,
      atRemoteId: (id) => players.get(Number(id)) || null,
      exists: (player) => player instanceof Player && players.get(player.id) === player,
      toArray: () => [...players.values()],
      forEach: (fn) => { for (const player of players.values()) fn(player, player.id); },
      get length() { return players.size; },
      call(first, second, third) {
        const selected = Array.isArray(first) ? first : [...players.values()];
        const name = Array.isArray(first) ? second : first;
        const args = Array.isArray(first) ? third : second;
        for (const player of selected) {
          if (players.get(player?.id) === player) player.call(name, args || []);
        }
      },
    },
  };

  alt.on('flovmp:native:ready', (id, name) => {
    id = Number(id);
    if (!Number.isInteger(id) || id < 1 || players.has(id)) return;
    const player = new Player(id, String(name));
    players.set(id, player);
    dispatch('playerJoin', player);
    dispatch('playerReady', player);
  });
  alt.on('flovmp:native:left', (id) => {
    const player = players.get(Number(id));
    if (!player) return;
    players.delete(player.id);
    dispatch('playerQuit', player);
  });
  alt.on('flovmp:client:event', (id, name, json) => {
    const player = players.get(Number(id));
    if (!player || typeof name !== 'string' || !EVENT_NAME.test(name)) return;
    try {
      const args = JSON.parse(json);
      if (Array.isArray(args)) dispatch(name, player, ...args);
    } catch { /* повреждённый payload не достигает обработчика геймода */ }
  });
  alt.on('flovmp:native:command', (id, _name, command, argsLine) => {
    const player = players.get(Number(id));
    const fn = commands.get(String(command).toLowerCase());
    if (player && fn) {
      const args = String(argsLine || '').trim();
      try { fn(player, args, ...args.split(/\s+/).filter(Boolean)); }
      catch (error) { alt.logError?.(`[RAGE compat] /${command}: ${error?.stack || error}`); }
    }
  });

  return mp;
}
