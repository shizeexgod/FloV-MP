'use strict';

const assert = require('node:assert/strict');
const http = require('node:http');
const { test } = require('node:test');
const { checkServerStatus } = require('../src/server-status');

async function withServer(handler, run) {
  const server = http.createServer(handler);
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  try { await run(server.address().port); }
  finally { await new Promise((resolve) => server.close(resolve)); }
}

test('reads real /info response', async () => {
  await withServer((_req, res) => {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ players: 42, maxPlayers: 2000 }));
  }, async (port) => {
    assert.deepEqual(await checkServerStatus('127.0.0.1', port),
      { online: true, players: 42, maxPlayers: 2000 });
  });
});

test('falls back to TCP when /info is absent', async () => {
  await withServer((_req, res) => { res.writeHead(404); res.end(); }, async (port) => {
    assert.deepEqual(await checkServerStatus('127.0.0.1', port, { fallbackHttp: false }),
      { online: true, players: 1, maxPlayers: 128 });
  });
});

test('a stalled /info does not hold other launcher work', async () => {
  await withServer(() => {}, async (port) => {
    const started = Date.now();
    const result = await checkServerStatus('127.0.0.1', port,
      { infoTimeout: 30, fallbackHttp: false });
    assert.equal(result.online, true);
    assert.ok(Date.now() - started < 1000);
  });
});

test('rejects invalid host and port without networking', async () => {
  assert.equal((await checkServerStatus('localhost/evil', 7788)).online, false);
  assert.equal((await checkServerStatus('localhost', 65536)).online, false);
});
