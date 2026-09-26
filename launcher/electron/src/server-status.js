'use strict';

const http = require('node:http');
const net = require('node:net');

function readInfo(host, port, timeoutMs) {
  return new Promise((resolve) => {
    const req = http.get({ hostname: host, port, path: '/info', timeout: timeoutMs }, (res) => {
      if (res.statusCode !== 200) { res.resume(); resolve(null); return; }
      let body = '';
      res.setEncoding('utf8');
      res.on('data', (chunk) => {
        body += chunk;
        if (body.length > 65536) req.destroy();
      });
      res.on('end', () => {
        try {
          const info = JSON.parse(body);
          resolve({ online: true, players: Number(info.players) || 0,
            maxPlayers: Number(info.maxPlayers) || 128 });
        } catch { resolve(null); }
      });
    });
    req.on('timeout', () => req.destroy());
    req.on('error', () => resolve(null));
  });
}

function probeTcp(host, port, timeoutMs) {
  return new Promise((resolve) => {
    const socket = net.createConnection({ host, port });
    let settled = false;
    const finish = (online) => {
      if (settled) return;
      settled = true;
      socket.destroy();
      resolve({ online, players: online ? 1 : 0, maxPlayers: online ? 128 : 0 });
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false));
    socket.once('error', () => finish(false));
  });
}

async function checkServerStatus(host, port, options = {}) {
  if (typeof host !== 'string' || !/^[a-zA-Z0-9.:-]{1,253}$/.test(host)) {
    return { online: false, players: 0, maxPlayers: 0 };
  }
  const gamePort = Number(port);
  if (!Number.isInteger(gamePort) || gamePort < 1 || gamePort > 65535) {
    return { online: false, players: 0, maxPlayers: 0 };
  }
  const infoTimeout = options.infoTimeout ?? 800;
  const tcpTimeout = options.tcpTimeout ?? 1500;
  const info = await readInfo(host, gamePort, infoTimeout);
  if (info) return info;
  if (options.fallbackHttp !== false && gamePort !== 80) {
    const fallback = await readInfo(host, 80, infoTimeout);
    if (fallback) return fallback;
  }
  return probeTcp(host, gamePort, tcpTimeout);
}

module.exports = { checkServerStatus };
