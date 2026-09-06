'use strict';

const { spawn } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');
const readline = require('node:readline');
const { EventEmitter } = require('node:events');

/**
 * Держит один долгоживущий процесс FloVMP.Launcher.Native.exe и говорит с ним
 * по NDJSON: одна строка запроса {"id":N,"cmd":"..."} -> одна строка ответа
 * {"id":N,"ok":true|false,"result"|"error":...}. Вся Windows-специфичная логика
 * (реестр, поиск GTA V, запуск через FloVMP.Connect) остаётся в .NET —
 * этот модуль только маршрутизирует запросы/ответы.
 */
class NativeBridge extends EventEmitter {
  constructor() {
    super();
    this.proc = null;
    this.nextId = 1;
    this.pending = new Map();
  }

  _resolveExePath() {
    const candidates = [
      // Упакованное приложение: FloVMP.Launcher.Native.exe лежит в resources/native
      path.join(process.resourcesPath || '', 'native', 'FloVMP.Launcher.Native.exe'),
      // Разработка: собранный .NET-проект рядом
      path.join(__dirname, '..', '..', 'src', 'FloVMP.Launcher.Native',
        'bin', 'Release', 'net8.0-windows', 'FloVMP.Launcher.Native.exe'),
      path.join(__dirname, '..', '..', 'src', 'FloVMP.Launcher.Native',
        'bin', 'Debug', 'net8.0-windows', 'FloVMP.Launcher.Native.exe'),
    ];
    return candidates.find((p) => p && fs.existsSync(p)) || null;
  }

  start() {
    const exePath = this._resolveExePath();
    if (!exePath) {
      throw new Error('FloVMP.Launcher.Native.exe не найден — соберите launcher/src/FloVMP.Launcher.Native.');
    }

    this.proc = spawn(exePath, [], { windowsHide: true });

    const rl = readline.createInterface({ input: this.proc.stdout });
    rl.on('line', (line) => {
      if (!line.trim()) return;
      let msg;
      try { msg = JSON.parse(line); } catch { return; }

      // Unprompted broadcast events from native process (e.g. download progress, game exit)
      if (msg.event) {
        this.emit(msg.event, msg);
        this.emit('event', msg.event, msg);
        return;
      }

      if (msg.id != null) {
        const waiter = this.pending.get(msg.id);
        if (!waiter) return;
        this.pending.delete(msg.id);
        if (msg.ok) waiter.resolve(msg.result);
        else waiter.reject(new Error(msg.error || 'native error'));
      }
    });

    this.proc.on('exit', (code) => {
      for (const waiter of this.pending.values()) waiter.reject(new Error('native helper exited'));
      this.pending.clear();
      this.proc = null;
    });

    this.proc.stderr.on('data', (chunk) => {
      console.error('[native]', chunk.toString());
    });
  }

  call(cmd, args = {}) {
    if (!this.proc) throw new Error('native helper is not running');
    const id = this.nextId++;
    const payload = JSON.stringify({ id, cmd, ...args });
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.proc.stdin.write(payload + '\n');
    });
  }

  stop() {
    if (this.proc) {
      this.proc.kill();
      this.proc = null;
    }
  }
}

module.exports = { NativeBridge };
