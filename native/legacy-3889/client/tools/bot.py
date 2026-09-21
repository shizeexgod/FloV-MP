"""Тестовый игрок FloV:MP (протокол FLOV/2) без GTA.

Подключается к шлюзу b3889 так же, как настоящий клиент (ECDSA-подпись),
ходит по кругу у точки спавна, отвечает на команды сервера и пишет в журнал
всё, что получает. Нужен для сквозной проверки сервера и как «второй игрок»,
когда на машине запущена только одна GTA.

  python tools/bot.py --host 127.0.0.1 --port 7798 --name Bot1 [--say "привет"] [--seconds 30]
  python tools/bot.py --check   # короткая самопроверка: вход, спавн, чат, /help — код выхода 0/1
"""
import argparse
import base64
import math
import os
import socket
import sys
import threading
import time

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

GAME_VERSION = "1.0.3889.0"


def esc(v):
    return str(v).replace("\\", "\\\\").replace("\t", "\\t").replace("\n", "\\n").replace("\r", "\\r")


def unesc(v):
    out, i = [], 0
    while i < len(v):
        c = v[i]
        if c == "\\" and i + 1 < len(v):
            n = v[i + 1]
            out.append({"t": "\t", "n": "\n", "r": "\r"}.get(n, n))
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


def fmt(*fields):
    return fields[0] + "".join("\t" + esc(f) for f in fields[1:])


class Bot:
    def __init__(self, host, port, name, key_path=None, log=print):
        self.host, self.port, self.name, self.log = host, port, name, log
        self.key = self._load_key(key_path)
        self.sock = None
        self.buf = b""
        self.id = 0
        self.spawn = None
        self.messages = []
        self.alive = False
        self.lock = threading.Lock()
        self.pos = [0.0, 0.0, 0.0]
        self.heading = 0.0

    @staticmethod
    def _load_key(path):
        if path and os.path.exists(path):
            return serialization.load_pem_private_key(open(path, "rb").read(), None)
        key = ec.generate_private_key(ec.SECP256R1())
        if path:
            open(path, "wb").write(key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8,
                                                     serialization.NoEncryption()))
        return key

    def public_raw(self):
        nums = self.key.public_key().public_numbers()
        return nums.x.to_bytes(32, "big") + nums.y.to_bytes(32, "big")

    def send(self, *fields):
        data = (fmt(*fields) + "\n").encode("utf-8")
        with self.lock:
            self.sock.sendall(data)

    def read_line(self, timeout=10):
        self.sock.settimeout(timeout)
        while b"\n" not in self.buf:
            chunk = self.sock.recv(65536)
            if not chunk:
                return None
            self.buf += chunk
        line, self.buf = self.buf.split(b"\n", 1)
        return [p if i == 0 else unesc(p) for i, p in enumerate(line.decode("utf-8").rstrip("\r").split("\t"))]

    def connect(self):
        self.sock = socket.create_connection((self.host, self.port), timeout=10)
        self.sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        pub = self.public_raw()
        self.send("HELLO", "2", GAME_VERSION, "bot-1.0", self.name, base64.b64encode(pub).decode(),
                  "B0B0B0B0B0B0B0B0", "0000000000000B07")
        ch = self.read_line()
        if not ch or ch[0] != "CHALLENGE":
            raise RuntimeError("вход отклонён: %s" % ch)
        nonce = base64.b64decode(ch[1])
        der = self.key.sign(b"FLOVMP-AUTH-v2" + nonce + pub, ec.ECDSA(hashes.SHA256()))
        r, s = decode_dss_signature(der)
        self.send("AUTH", base64.b64encode(r.to_bytes(32, "big") + s.to_bytes(32, "big")).decode())
        first = self.read_line()
        if not first or first[0] != "WELCOME":
            raise RuntimeError("вход отклонён: %s" % first)
        self.id = int(first[1])
        self.voice_token = first[5] if len(first) > 5 else ""
        self.voice_port = int(first[6]) if len(first) > 6 and first[6] else 0
        self.alive = True
        self.log("[%s] WELCOME id=%s identity=%s" % (self.name, first[1], first[3]))
        threading.Thread(target=self._reader, daemon=True).start()

    def _reader(self):
        while self.alive:
            try:
                m = self.read_line(timeout=60)
            except socket.timeout:
                continue  # тишина от сервера — не разрыв (нет других игроков рядом)
            except OSError:
                break
            if m is None:
                break
            self.messages.append(m)
            t = m[0]
            if t == "SPAWN":
                self.spawn = [float(m[1]), float(m[2]), float(m[3])]
                self.pos = list(self.spawn)
            elif t in ("TP",):
                self.pos = [float(m[1]), float(m[2]), float(m[3])]
            if t not in ("PSTATE", "PONG"):
                self.log("[%s] <- %s" % (self.name, " | ".join(m)))
        self.alive = False
        self.log("[%s] соединение закрыто" % self.name)

    def state(self, speed=1.0):
        x, y, z = self.pos
        self.send("STATE", x, y, z, self.heading, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 200, 0, 0, speed, 1885233650)

    def drive(self, seconds, model):
        """Едет по кругу радиусом 12 м на машине model — проверка синхронизации транспорта."""
        t0 = time.time()
        while not self.spawn and time.time() - t0 < 5:
            time.sleep(0.05)
        self.send("READY")
        cx, cy, cz = self.spawn or self.pos
        w = 0.4
        while self.alive and time.time() - t0 < seconds:
            a = (time.time() - t0) * w
            x, y = cx + math.cos(a) * 12, cy + math.sin(a) * 12
            vx, vy = -math.sin(a) * 12 * w, math.cos(a) * 12 * w
            heading = (math.degrees(a) + 180) % 360  # GTA: курс 0 = север, против часовой
            self.pos = [x, y, cz]
            self.send("STATE", x, y, cz, heading, vx, vy, 0, 1 | 256, model, self.id, -1, 0, 0, heading,
                      200, 0, 0, 10, 1885233650)
            time.sleep(0.05)

    def hit(self, target, times, damage):
        for _ in range(times):
            self.send("HIT", target, 453432689, damage)  # weapon_pistol
            time.sleep(0.3)

    def walk(self, seconds, say=None):
        t0 = time.time()
        while not self.spawn and time.time() - t0 < 5:
            time.sleep(0.05)
        self.send("READY")
        center = list(self.spawn or self.pos)
        said = False
        while self.alive and time.time() - t0 < seconds:
            a = (time.time() - t0) * 0.35
            self.pos = [center[0] + math.cos(a) * 4, center[1] + math.sin(a) * 4, center[2]]
            self.heading = (math.degrees(a) + 90) % 360
            self.state(1.0)
            if say and not said and time.time() - t0 > 1.5:
                self.send("CHAT", say)
                said = True
            time.sleep(0.05)

    def voice(self, seconds, opus_dll):
        """Голос: шлёт тон 440 Гц (Opus из opus.dll игры) и считает принятые голоса."""
        import ctypes, struct, socket as so
        first = [m for m in self.messages if m[0] == "WELCOME"]
        token, port = self.voice_token, self.voice_port
        if not token or not port:
            raise RuntimeError("сервер не выдал токен голоса")
        opus = ctypes.CDLL(opus_dll)
        opus.opus_encoder_create.restype = ctypes.c_void_p
        opus.opus_encode.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_int]
        err = ctypes.c_int()
        enc = opus.opus_encoder_create(48000, 1, 2048, ctypes.byref(err))
        udp = so.socket(so.AF_INET, so.SOCK_DGRAM)
        udp.settimeout(0.001)
        dest = (self.host, port)
        tok = struct.pack("<Q", int(token, 16))
        udp.sendto(tok + struct.pack("<H", 0), dest)
        self.voice_received = {}
        frame = (ctypes.c_short * 960)()
        out = (ctypes.c_ubyte * 400)()
        t0, seq, phase = time.time(), 0, 0.0
        self.send("READY")
        while self.alive and time.time() - t0 < seconds:
            for i in range(960):
                frame[i] = int(8000 * math.sin(phase)); phase += 2 * math.pi * 440 / 48000
            n = opus.opus_encode(enc, frame, 960, out, 400)
            seq = (seq + 1) & 0xFFFF
            if n > 0:
                udp.sendto(tok + struct.pack("<H", seq) + bytes(out[:n]), dest)
            self.state(0)
            end = time.time() + 0.02
            while time.time() < end:
                try:
                    data, _ = udp.recvfrom(2000)
                    sid = struct.unpack("<I", data[:4])[0]
                    self.voice_received[sid] = self.voice_received.get(sid, 0) + 1
                except (so.timeout, OSError):
                    pass
        return self.voice_received

    def close(self):
        self.alive = False
        try:
            self.sock.close()
        except OSError:
            pass


def check(host, port):
    ok = True
    bot = Bot(host, port, "CheckBot", log=lambda s: None)
    bot.connect()
    other = Bot(host, port, "CheckBot2", log=lambda s: None)
    other.connect()
    threading.Thread(target=other.walk, args=(6,), daemon=True).start()
    bot.walk(4, say="проверка чата")
    bot.send("CHAT", "/help")
    time.sleep(1.5)
    types = [m[0] for m in bot.messages]
    for need in ("SPAWN", "ADMIN", "CMDS", "MSG", "PADD", "PSTATE"):
        good = need in types
        ok &= good
        print(("OK   " if good else "FAIL ") + need)
    chat_seen = any(m[0] == "MSG" and "проверка чата" in m[-1] for m in other.messages)
    print(("OK   " if chat_seen else "FAIL ") + "чат доходит до другого игрока")
    help_seen = any(m[0] == "MSG" and "КОМАНД" in m[-1] for m in bot.messages)
    print(("OK   " if help_seen else "FAIL ") + "/help отвечает")
    ok &= chat_seen and help_seen
    bot.close()
    time.sleep(0.5)
    left = any(m[0] == "PDEL" and m[1] == str(bot.id) for m in other.messages)
    print(("OK   " if left else "FAIL ") + "выход игрока виден остальным (PDEL)")
    ok &= left
    other.close()
    return ok


def identity_of(key_path):
    """ID игрока (как его видит сервер) по файлу ключа бота."""
    import hashlib
    b = Bot("-", 0, "-", key_path, log=lambda s: None)
    h = hashlib.sha256(b.public_raw()).digest()
    return (1 << 63) | (int.from_bytes(h[:8], "little") & ((1 << 63) - 1))


def wait_for(bot, pred, timeout=5):
    t0 = time.time()
    while time.time() - t0 < timeout:
        for m in list(bot.messages):
            if pred(m):
                return m
        time.sleep(0.05)
    return None


def moderation(host, port, admin_key):
    """Модерация глазами игроков: kick, ban, отказ при входе, unban, mute."""
    ok = True

    def check(name, cond):
        nonlocal ok
        ok &= bool(cond)
        print(("OK   " if cond else "FAIL ") + name)

    admin = Bot(host, port, "AdminBot", admin_key, log=lambda s: None)
    admin.connect()
    admin.send("READY")
    check("админ-бот получил уровень 8", wait_for(admin, lambda m: m[0] == "ADMIN" and m[1] == "8"))

    victim_key = os.path.join(os.path.dirname(admin_key), "victim.pem")
    if os.path.exists(victim_key):
        os.remove(victim_key)
    victim = Bot(host, port, "Victim", victim_key, log=lambda s: None)
    victim.connect()
    victim.send("READY")
    time.sleep(0.5)

    admin.send("CHAT", "/kick %d проверка кика" % victim.id)
    check("кик доходит до игрока с причиной", wait_for(victim, lambda m: m[0] == "KICK" and "проверка кика" in m[1]))
    time.sleep(1)

    victim = Bot(host, port, "Victim", victim_key, log=lambda s: None)
    victim.connect()
    victim.send("READY")
    time.sleep(0.5)
    admin.send("CHAT", "/mute %d 5" % victim.id)
    time.sleep(0.5)
    victim.send("CHAT", "это сообщение не должно дойти")
    time.sleep(0.8)
    check("мут: сообщение не разослано", not any(m[0] == "MSG" and "не должно дойти" in m[-1] for m in admin.messages))
    check("мут: игрок предупреждён", wait_for(victim, lambda m: m[0] == "MSG" and "заглушён" in m[-1]))
    admin.send("CHAT", "/unmute %d" % victim.id)
    time.sleep(0.5)

    admin.send("CHAT", "/ban %d 1 проверка бана" % victim.id)
    check("бан кикает с причиной", wait_for(victim, lambda m: m[0] == "KICK" and "проверка бана" in m[1]))
    time.sleep(1)

    again = Bot(host, port, "Victim", victim_key, log=lambda s: None)
    try:
        again.connect()
        check("забаненный не входит", False)
        again.close()
    except RuntimeError as e:
        check("забаненный не входит (до WELCOME)", "заблокирован" in str(e))

    admin.send("CHAT", "/unban Victim")
    check("unban подтверждён", wait_for(admin, lambda m: m[0] == "MSG" and "Снято блокировок" in m[-1]))
    time.sleep(0.5)
    back = Bot(host, port, "Victim", victim_key, log=lambda s: None)
    try:
        back.connect()
        check("после unban вход открыт", True)
        back.close()
    except RuntimeError as e:
        check("после unban вход открыт: " + str(e), False)
    admin.close()
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=7798)
    ap.add_argument("--name", default="Bot")
    ap.add_argument("--key", default=None, help="файл ключа (постоянный ID бота)")
    ap.add_argument("--say", default=None)
    ap.add_argument("--seconds", type=float, default=30)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--moderation-test", metavar="ADMIN_KEY", help="сценарий модерации; ключ бота-владельца")
    ap.add_argument("--identity", metavar="KEY", help="напечатать ID игрока для файла ключа (создаст ключ)")
    ap.add_argument("--drive", type=lambda v: int(v, 0), default=0, help="хэш модели машины (0xB779A091 = adder)")
    ap.add_argument("--hit", type=int, default=0, help="ID игрока: нанести урон (проверка PvP)")
    ap.add_argument("--voice", metavar="OPUS_DLL", help="говорить тоном 440 Гц (opus.dll из папки GTA) и считать чужой голос")
    a = ap.parse_args()
    if a.check:
        sys.exit(0 if check(a.host, a.port) else 1)
    if a.identity:
        print(identity_of(a.identity))
        return
    if a.moderation_test:
        sys.exit(0 if moderation(a.host, a.port, a.moderation_test) else 1)
    bot = Bot(a.host, a.port, a.name, a.key)
    bot.connect()
    if a.voice:
        got = bot.voice(a.seconds, a.voice)
        print("голос принят от игроков:", got)
        bot.close()
        return
    if a.hit:
        bot.send("READY")
        time.sleep(1)
        bot.pos = list(bot.spawn or bot.pos)
        bot.state(0)
        bot.hit(a.hit, 3, 30)
        time.sleep(1)
    elif a.drive:
        bot.drive(a.seconds, a.drive)
    else:
        bot.walk(a.seconds, a.say)
    bot.close()


if __name__ == "__main__":
    main()
