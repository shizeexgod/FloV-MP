#include <winsock2.h>
#include <ws2tcpip.h>
#include "net.h"
#include "common.h"
#include "identity.h"

#pragma comment(lib, "ws2_32.lib")

namespace flov
{
    namespace
    {
        constexpr size_t kMaxLine = 64 * 1024;

        class LineSocket
        {
        public:
            explicit LineSocket(SOCKET s) : _s(s) {}
            /// Следующая строка; false — соединение закрыто или ошибка.
            bool ReadLine(std::string& line)
            {
                for (;;)
                {
                    const auto nl = _buf.find('\n');
                    if (nl != std::string::npos)
                    {
                        line = _buf.substr(0, nl);
                        if (!line.empty() && line.back() == '\r') line.pop_back();
                        _buf.erase(0, nl + 1);
                        return true;
                    }
                    if (_buf.size() > kMaxLine) return false;
                    char tmp[8192];
                    const int n = recv(_s, tmp, sizeof tmp, 0);
                    if (n <= 0) return false;
                    g_bytesIn += (uint64_t)n;
                    _buf.append(tmp, n);
                }
            }
        private:
            SOCKET _s;
            std::string _buf;
        };
    }

    Net::~Net()
    {
        // Деструктор вызывается при выгрузке DLL под loader lock: ждать поток
        // здесь нельзя (взаимоблокировка). Сокет закрыт — поток завершится сам.
        Disconnect("выход из игры");
        if (_thread.joinable()) _thread.detach();
    }

    void Net::SetStatus(const std::string& s)
    {
        {
            std::lock_guard lock(_mutex);
            _status = s;
        }
        Log("net: " + s);
    }

    std::string Net::Status() const
    {
        std::lock_guard lock(_mutex);
        return _status;
    }

    std::string Net::PeerIp() const
    {
        std::lock_guard lock(_mutex);
        return _peerIp;
    }

    std::string Net::Endpoint() const
    {
        std::lock_guard lock(_mutex);
        return _endpoint;
    }

    void Net::Connect(const std::string& host, int port, const std::string& name)
    {
        Disconnect("переподключение");
        if (_thread.joinable()) _thread.join();
        _stop = false;
        {
            std::lock_guard lock(_mutex);
            _inbound.clear();
            _endpoint = host + ":" + std::to_string(port);
        }
        _state = NetState::Connecting;
        _thread = std::thread(&Net::Run, this, host, port, name);
    }

    void Net::Disconnect(const std::string& reason)
    {
        _stop = true;
        const auto s = _socket.exchange(~0ull);
        if (s != ~0ull)
        {
            shutdown((SOCKET)s, SD_BOTH);
            closesocket((SOCKET)s);
            Log("net: отключение — " + reason);
        }
    }

    bool Net::SendRaw(const std::string& line)
    {
        const auto s = _socket.load();
        if (s == ~0ull) return false;
        std::lock_guard lock(_sendMutex);
        const std::string data = line + "\n";
        size_t sent = 0;
        while (sent < data.size())
        {
            const int n = send((SOCKET)s, data.data() + sent, (int)(data.size() - sent), 0);
            if (n <= 0) return false;
            sent += n;
            g_bytesOut += (uint64_t)n;
        }
        return true;
    }

    void Net::Send(const std::vector<std::string>& fields)
    {
        if (_state != NetState::Connected) return;
        SendRaw(Format(fields));
    }

    std::vector<std::vector<std::string>> Net::Poll()
    {
        std::lock_guard lock(_mutex);
        std::vector<std::vector<std::string>> out(_inbound.begin(), _inbound.end());
        _inbound.clear();
        return out;
    }

    void Net::Run(std::string host, int port, std::string name)
    {
        WSADATA wsa{};
        WSAStartup(MAKEWORD(2, 2), &wsa);
        auto fail = [this](const std::string& why) {
            SetStatus(why);
            _state = NetState::Failed;
            std::lock_guard lock(_mutex);
            _inbound.push_back({ "NET_FAILED", why });
        };

        Identity identity;
        std::string error;
        if (!identity.Load(error)) { fail("Ключ игрока: " + error); WSACleanup(); return; }

        SetStatus("Подключение к " + host + ":" + std::to_string(port) + "...");
        addrinfo hints{};
        hints.ai_family = AF_UNSPEC;
        hints.ai_socktype = SOCK_STREAM;
        hints.ai_protocol = IPPROTO_TCP;
        addrinfo* list = nullptr;
        if (getaddrinfo(host.c_str(), std::to_string(port).c_str(), &hints, &list) != 0 || !list)
        {
            fail("Адрес сервера не найден: " + host);
            WSACleanup();
            return;
        }
        SOCKET s = INVALID_SOCKET;
        for (auto* a = list; a && !_stop; a = a->ai_next)
        {
            s = socket(a->ai_family, a->ai_socktype, a->ai_protocol);
            if (s == INVALID_SOCKET) continue;
            DWORD timeout = 8000;
            setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, (const char*)&timeout, sizeof timeout);
            setsockopt(s, SOL_SOCKET, SO_SNDTIMEO, (const char*)&timeout, sizeof timeout);
            BOOL nodelay = TRUE;
            setsockopt(s, IPPROTO_TCP, TCP_NODELAY, (const char*)&nodelay, sizeof nodelay);
            if (connect(s, a->ai_addr, (int)a->ai_addrlen) == 0) break;
            closesocket(s);
            s = INVALID_SOCKET;
        }
        freeaddrinfo(list);
        if (s == INVALID_SOCKET)
        {
            fail("Сервер " + host + ":" + std::to_string(port) + " не отвечает. Проверьте адрес и что сервер запущен (порт TCP " + std::to_string(port) + ").");
            WSACleanup();
            return;
        }
        _socket = (unsigned long long)s;
        {
            sockaddr_storage peer{};
            int len = sizeof peer;
            char text[INET6_ADDRSTRLEN] = {};
            if (getpeername(s, (sockaddr*)&peer, &len) == 0)
            {
                if (peer.ss_family == AF_INET) inet_ntop(AF_INET, &((sockaddr_in*)&peer)->sin_addr, text, sizeof text);
                else if (peer.ss_family == AF_INET6) inet_ntop(AF_INET6, &((sockaddr_in6*)&peer)->sin6_addr, text, sizeof text);
            }
            std::lock_guard lock(_mutex);
            _peerIp = text;
            // IPv4 через IPv6-сокет: ::ffff:1.2.3.4 → 1.2.3.4
            if (_peerIp.rfind("::ffff:", 0) == 0) _peerIp = _peerIp.substr(7);
        }
        LineSocket reader(s);

        // Рукопожатие: HELLO → CHALLENGE → AUTH → WELCOME | REJECT.
        const auto hello = Format({ "HELLO", kProtocolVersion, GameVersion(), kClientVersion, name,
                                    identity.PublicKeyB64(), HardwareIdHex(), MacHashHex() });
        std::string line;
        if (!SendRaw(hello) || !reader.ReadLine(line))
        {
            fail("Сервер закрыл соединение при входе.");
            Disconnect("handshake");
            WSACleanup();
            return;
        }
        auto parts = Parse(line);
        if (parts[0] == "REJECT")
        {
            fail("Сервер отклонил вход: " + (parts.size() > 1 ? parts[1] : std::string("без причины")));
            Disconnect("reject");
            WSACleanup();
            return;
        }
        if (parts[0] != "CHALLENGE" || parts.size() < 2)
        {
            fail("Это не сервер FloV:MP (или несовместимая версия).");
            Disconnect("protocol");
            WSACleanup();
            return;
        }
        const auto nonce = FromBase64(parts[1]);
        const std::string domain = "FLOVMP-AUTH-v2";
        std::vector<unsigned char> message(domain.begin(), domain.end());
        message.insert(message.end(), nonce.begin(), nonce.end());
        message.insert(message.end(), identity.PublicKey().begin(), identity.PublicKey().end());
        SendRaw(Format({ "AUTH", identity.SignB64(message) }));

        // Дальше без таймаута чтения: сервер сам рвёт мёртвые соединения,
        // а клиент шлёт PING каждые 2 секунды.
        DWORD noTimeout = 0;
        setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, (const char*)&noTimeout, sizeof noTimeout);

        bool welcomed = false;
        std::string kickReason;
        // Меню паузы останавливает скрипты игры, а с ними STATE и PING. Без
        // отдельного пульса сервер через 30 с счёл бы игрока пропавшим.
        std::thread keepalive([this] {
            for (int tick = 0; !_stop; ++tick)
            {
                Sleep(250);
                if (tick % 20 == 19 && !SendRaw("KEEPALIVE")) break;
            }
        });
        while (!_stop && reader.ReadLine(line))
        {
            auto msg = Parse(line);
            if (msg.empty()) continue;
            if (msg[0] == "WELCOME" && !welcomed)
            {
                welcomed = true;
                _state = NetState::Connected;
                SetStatus("Подключено к " + host + ":" + std::to_string(port));
            }
            else if (msg[0] == "REJECT" || msg[0] == "KICK")
            {
                kickReason = msg.size() > 1 ? msg[1] : "";
            }
            std::lock_guard lock(_mutex);
            if (_inbound.size() < 20000) _inbound.push_back(std::move(msg));
        }

        const bool userStop = _stop.load();
        Disconnect("поток завершён");
        if (keepalive.joinable()) keepalive.join();
        _state = NetState::Failed;
        const auto why = !kickReason.empty() ? kickReason
                       : userStop ? std::string("Отключено.")
                       : welcomed ? std::string("Соединение с сервером потеряно.")
                       : std::string("Сервер закрыл соединение при входе.");
        SetStatus(why);
        {
            std::lock_guard lock(_mutex);
            _inbound.push_back({ "NET_CLOSED", why, kickReason.empty() ? "0" : "1" });
        }
        WSACleanup();
    }
}
