import assert from 'node:assert/strict';
import test from 'node:test';
import { createRageCompat } from './compat.mjs';

function fixture() {
  const listeners = new Map();
  const emitted = [];
  const alt = {
    on(name, fn) { listeners.set(name, fn); },
    emit(...args) { emitted.push(args); },
    logError(message) { throw new Error(message); },
  };
  const mp = createRageCompat(alt);
  return { mp, emitted, fire: (name, ...args) => listeners.get(name)(...args) };
}

test('join, remote event, player.call and leave form a complete path', () => {
  const { mp, emitted, fire } = fixture();
  const joined = [];
  mp.events.add('playerJoin', player => joined.push(player));
  fire('flovmp:native:ready', 7, 'Ivan');
  const player = mp.players.at(7);
  assert.equal(player, joined[0]);
  assert.equal(mp.players.length, 1);
  let received;
  mp.events.add('buy', (from, model, price) => { received = [from, model, price]; });
  fire('flovmp:client:event', 7, 'buy', '["adder",1500]');
  assert.deepEqual(received, [player, 'adder', 1500]);
  player.call('hud:money', [1500]);
  assert.deepEqual(emitted.at(-1), ['flovmp:client:call', 7, 'hud:money', '[1500]']);
  let left;
  mp.events.add('playerQuit', p => { left = p; });
  fire('flovmp:native:left', 7);
  assert.equal(left, player);
  assert.equal(mp.players.at(7), null);
});

test('variables, broadcast, commands and invalid remote JSON', () => {
  const { mp, emitted, fire } = fixture();
  fire('flovmp:native:ready', 1, 'A');
  fire('flovmp:native:ready', 2, 'B');
  const p = mp.players.at(1);
  p.setVariable('job', 'taxi');
  assert.equal(p.getVariable('job'), 'taxi');
  assert.deepEqual(emitted.at(-1), ['flovmp:player:setVariable', 1, 'job', '"taxi"']);
  p.setVariable('job', null);
  assert.equal(p.hasVariable('job'), false);
  mp.players.call('notice', ['hello']);
  assert.equal(emitted.filter(x => x[0] === 'flovmp:client:call').length, 2);
  let called = 0;
  mp.events.add('buy', () => called++);
  fire('flovmp:client:event', 1, 'buy', '{bad');
  fire('flovmp:client:event', 999, 'buy', '[]');
  assert.equal(called, 0);
  let command;
  mp.events.addCommand('test', (player, full, value) => { command = [player.id, full, value]; });
  fire('flovmp:native:command', 1, 'A', 'test', 'one two');
  assert.deepEqual(command, [1, 'one two', 'one']);
  assert.deepEqual(emitted.at(-1), ['flovmp:commands:register', 'test', 'RAGE:MP /test', 0]);
});

test('wire limits apply after FLOV/2 escaping', () => {
  const { mp, fire } = fixture();
  fire('flovmp:native:ready', 1, 'A');
  const p = mp.players.at(1);
  assert.throws(() => p.call('x', ['\\'.repeat(2200)]), RangeError);
  assert.throws(() => p.setVariable('x', '\\'.repeat(2200)), RangeError);
  assert.throws(() => p.call('bad\tname', []), TypeError);
});
