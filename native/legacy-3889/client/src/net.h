#pragma once
#include <atomic>
#include <deque>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace flov
{
    enum class NetState { Idle, Connecting, Connected, Failed };

    /// Соединение с сервером FloV:MP (FLOV/2 поверх TCP).
    /// Свой поток: подключение, рукопожатие с подписью, приём и отправка.
    /// Игровой поток только забирает сообщения (Poll) и кладёт свои (Send).
    class Net
    {
    public:
        ~Net();
        void Connect(const std::string& host, int port, const std::string& name);
        void Disconnect(const std::string& reason);
        void Send(const std::vector<std::string>& fields);
        /// Забрать все пришедшие сообщения.
        std::vector<std::vector<std::string>> Poll();

        NetState State() const { return _state.load(); }
        std::string Status() const;
        std::string Endpoint() const;
        int PingMs() const { return _ping.load(); }
        void SetPing(int ms) { _ping = ms; }

    private:
        void Run(std::string host, int port, std::string name);
        void SetStatus(const std::string& s);
        bool SendRaw(const std::string& line);

        std::thread _thread;
        std::atomic<NetState> _state{ NetState::Idle };
        std::atomic<bool> _stop{ false };
        std::atomic<unsigned long long> _socket{ ~0ull };
        std::atomic<int> _ping{ 0 };
        mutable std::mutex _mutex;
        std::deque<std::vector<std::string>> _inbound;
        std::string _status;
        std::string _endpoint;
        std::mutex _sendMutex;
    };
}
