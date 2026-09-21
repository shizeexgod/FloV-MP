#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <mmsystem.h>
#include "voice.h"
#include "common.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <deque>
#include <map>
#include <mutex>
#include <thread>

#pragma comment(lib, "winmm.lib")
#pragma comment(lib, "ws2_32.lib")

namespace flov::voice
{
    namespace
    {
        constexpr int kRate = 48000;
        constexpr int kFrame = 960;            // 20 мс
        constexpr int kMaxPacket = 400;
        constexpr int kCaptureBuffers = 6;
        constexpr int kPlayBuffers = 4;         // 80 мс вперёд
        constexpr size_t kMaxQueuedFrames = 8;  // 160 мс — дальше отстаём, сбрасываем
        constexpr int OPUS_APPLICATION_VOIP = 2048;
        constexpr int OPUS_SET_BITRATE_REQUEST = 4002;
        constexpr int OPUS_SET_INBAND_FEC_REQUEST = 4012;

        using EncCreate = void* (*)(int, int, int, int*);
        using DecCreate = void* (*)(int, int, int*);
        using Encode = int (*)(void*, const short*, int, unsigned char*, int);
        using Decode = int (*)(void*, const unsigned char*, int, short*, int, int);
        using Destroy = void (*)(void*);
        using Ctl = int (*)(void*, int, ...);

        struct Opus
        {
            HMODULE lib = nullptr;
            EncCreate encCreate = nullptr; DecCreate decCreate = nullptr;
            Encode encode = nullptr; Decode decode = nullptr;
            Destroy encDestroy = nullptr; Destroy decDestroy = nullptr;
            Ctl encCtl = nullptr;
            bool Load()
            {
                if (lib) return true;
                lib = GetModuleHandleW(L"opus.dll");
                if (!lib) lib = LoadLibraryW(L"opus.dll");
                if (!lib) return false;
                encCreate = (EncCreate)GetProcAddress(lib, "opus_encoder_create");
                decCreate = (DecCreate)GetProcAddress(lib, "opus_decoder_create");
                encode = (Encode)GetProcAddress(lib, "opus_encode");
                decode = (Decode)GetProcAddress(lib, "opus_decode");
                encDestroy = (Destroy)GetProcAddress(lib, "opus_encoder_destroy");
                decDestroy = (Destroy)GetProcAddress(lib, "opus_decoder_destroy");
                encCtl = (Ctl)GetProcAddress(lib, "opus_encoder_ctl");
                return encCreate && decCreate && encode && decode && encDestroy && decDestroy && encCtl;
            }
        } g_opus;

        struct Speaker
        {
            void* decoder = nullptr;
            std::deque<std::vector<short>> frames;
            float gain = 0.f, pan = 0.f;
            ULONGLONG lastPacket = 0;
            uint16_t lastSeq = 0;
            bool buffering = true; // копим 2 кадра перед началом — против дрожания сети
        };

        std::mutex g_mutex;
        std::map<uint32_t, Speaker> g_speakers;
        std::atomic<bool> g_running{ false }, g_talking{ false }, g_micOk{ false };
        std::string g_error;
        SOCKET g_sock = INVALID_SOCKET;
        sockaddr_storage g_server{};
        int g_serverLen = 0;
        uint64_t g_token = 0;
        std::thread g_captureThread, g_recvThread, g_playThread;

        void SetError(const std::string& e)
        {
            std::lock_guard lock(g_mutex);
            g_error = e;
            Log("голос: " + e);
        }

        void SendPacket(const unsigned char* opus, int len, uint16_t seq)
        {
            unsigned char buf[10 + kMaxPacket];
            memcpy(buf, &g_token, 8);
            memcpy(buf + 8, &seq, 2);
            if (len > 0) memcpy(buf + 10, opus, len);
            sendto(g_sock, (const char*)buf, 10 + len, 0, (sockaddr*)&g_server, g_serverLen);
        }

        // --- захват микрофона --------------------------------------------------------
        void CaptureLoop()
        {
            int err = 0;
            void* enc = g_opus.encCreate(kRate, 1, OPUS_APPLICATION_VOIP, &err);
            if (!enc || err != 0) { SetError("не удалось создать кодировщик Opus"); return; }
            g_opus.encCtl(enc, OPUS_SET_BITRATE_REQUEST, 24000);
            g_opus.encCtl(enc, OPUS_SET_INBAND_FEC_REQUEST, 1);

            HANDLE event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            WAVEFORMATEX fmt{ WAVE_FORMAT_PCM, 1, kRate, kRate * 2, 2, 16, 0 };
            HWAVEIN in = nullptr;
            const MMRESULT open = waveInOpen(&in, WAVE_MAPPER, &fmt, (DWORD_PTR)event, 0, CALLBACK_EVENT);
            std::vector<std::vector<short>> data(kCaptureBuffers, std::vector<short>(kFrame));
            std::vector<WAVEHDR> hdr(kCaptureBuffers);
            if (open != MMSYSERR_NOERROR)
            {
                SetError(open == MMSYSERR_BADDEVICEID || open == MMSYSERR_NODRIVER
                    ? "микрофон не найден — голос работает только на приём"
                    : "микрофон недоступен (Параметры Windows → Конфиденциальность → Микрофон) — голос работает только на приём");
            }
            else
            {
                for (int i = 0; i < kCaptureBuffers; ++i)
                {
                    hdr[i] = WAVEHDR{};
                    hdr[i].lpData = (LPSTR)data[i].data();
                    hdr[i].dwBufferLength = kFrame * 2;
                    waveInPrepareHeader(in, &hdr[i], sizeof(WAVEHDR));
                    waveInAddBuffer(in, &hdr[i], sizeof(WAVEHDR));
                }
                waveInStart(in);
                g_micOk = true;
            }

            uint16_t seq = 0;
            ULONGLONG lastKeepalive = 0;
            unsigned char packet[kMaxPacket];
            while (g_running)
            {
                const ULONGLONG now = GetTickCount64();
                if (now - lastKeepalive > 2000)
                {
                    lastKeepalive = now;
                    SendPacket(nullptr, 0, 0); // «я здесь»: сервер узнаёт наш UDP-адрес
                }
                if (!in) { Sleep(100); continue; }
                if (WaitForSingleObject(event, 100) != WAIT_OBJECT_0) continue;
                for (auto& h : hdr)
                {
                    if (!(h.dwFlags & WHDR_DONE)) continue;
                    if (g_talking && h.dwBytesRecorded == kFrame * 2)
                    {
                        const int n = g_opus.encode(enc, (const short*)h.lpData, kFrame, packet, sizeof packet);
                        if (n > 0) SendPacket(packet, n, ++seq);
                    }
                    h.dwFlags &= ~WHDR_DONE;
                    waveInAddBuffer(in, &h, sizeof(WAVEHDR));
                }
            }
            if (in)
            {
                waveInStop(in);
                waveInReset(in);
                for (auto& h : hdr) waveInUnprepareHeader(in, &h, sizeof(WAVEHDR));
                waveInClose(in);
            }
            CloseHandle(event);
            g_opus.encDestroy(enc);
        }

        // --- приём ---------------------------------------------------------------------
        void ReceiveLoop()
        {
            char buf[1500];
            std::vector<short> pcm(kFrame);
            while (g_running)
            {
                const int n = recv(g_sock, buf, sizeof buf, 0);
                if (n <= 0) { if (!g_running) break; Sleep(5); continue; }
                if (n < 6 + 1) continue;
                uint32_t id; uint16_t seq;
                memcpy(&id, buf, 4);
                memcpy(&seq, buf + 4, 2);
                std::lock_guard lock(g_mutex);
                auto& s = g_speakers[id];
                if (!s.decoder)
                {
                    int err = 0;
                    s.decoder = g_opus.decCreate(kRate, 1, &err);
                    if (!s.decoder) continue;
                    Log("голос: слышу игрока " + std::to_string(id));
                }
                // Потерянный кадр — восстановить из FEC следующего.
                if (s.lastPacket && (uint16_t)(seq - s.lastSeq) == 2)
                {
                    if (g_opus.decode(s.decoder, (const unsigned char*)buf + 6, n - 6, pcm.data(), kFrame, 1) == kFrame)
                        s.frames.push_back(pcm);
                }
                const int samples = g_opus.decode(s.decoder, (const unsigned char*)buf + 6, n - 6, pcm.data(), kFrame, 0);
                if (samples != kFrame) continue;
                s.lastSeq = seq;
                s.lastPacket = GetTickCount64();
                s.frames.push_back(pcm);
                while (s.frames.size() > kMaxQueuedFrames) s.frames.pop_front();
            }
        }

        // --- воспроизведение --------------------------------------------------------------
        void PlayLoop()
        {
            HANDLE event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            WAVEFORMATEX fmt{ WAVE_FORMAT_PCM, 2, kRate, kRate * 4, 4, 16, 0 };
            HWAVEOUT out = nullptr;
            if (waveOutOpen(&out, WAVE_MAPPER, &fmt, (DWORD_PTR)event, 0, CALLBACK_EVENT) != MMSYSERR_NOERROR)
            {
                SetError("нет устройства вывода звука — голоса других не слышно");
                CloseHandle(event);
                return;
            }
            std::vector<std::vector<short>> data(kPlayBuffers, std::vector<short>(kFrame * 2));
            std::vector<WAVEHDR> hdr(kPlayBuffers);
            for (int i = 0; i < kPlayBuffers; ++i)
            {
                hdr[i] = WAVEHDR{};
                hdr[i].lpData = (LPSTR)data[i].data();
                hdr[i].dwBufferLength = kFrame * 4;
                waveOutPrepareHeader(out, &hdr[i], sizeof(WAVEHDR));
                hdr[i].dwFlags |= WHDR_DONE; // свободен
            }
            std::vector<float> mix(kFrame * 2);
            while (g_running)
            {
                WaitForSingleObject(event, 25);
                for (int i = 0; i < kPlayBuffers; ++i)
                {
                    auto& h = hdr[i];
                    if (!(h.dwFlags & WHDR_DONE)) continue;
                    std::fill(mix.begin(), mix.end(), 0.f);
                    {
                        std::lock_guard lock(g_mutex);
                        for (auto& [id, s] : g_speakers)
                        {
                            if (s.buffering) { if (s.frames.size() >= 2) s.buffering = false; else continue; }
                            if (s.frames.empty()) { s.buffering = true; continue; }
                            const auto frame = std::move(s.frames.front());
                            s.frames.pop_front();
                            if (s.gain <= 0.001f) continue;
                            const float l = s.gain * std::sqrt((1.f - s.pan) * 0.5f);
                            const float r = s.gain * std::sqrt((1.f + s.pan) * 0.5f);
                            for (int k = 0; k < kFrame; ++k)
                            {
                                mix[2 * k] += frame[k] * l;
                                mix[2 * k + 1] += frame[k] * r;
                            }
                        }
                    }
                    for (int k = 0; k < kFrame * 2; ++k)
                        data[i][k] = (short)std::clamp(mix[k], -32768.f, 32767.f);
                    h.dwFlags &= ~WHDR_DONE;
                    waveOutWrite(out, &h, sizeof(WAVEHDR));
                }
            }
            waveOutReset(out);
            for (auto& h : hdr) waveOutUnprepareHeader(out, &h, sizeof(WAVEHDR));
            waveOutClose(out);
            CloseHandle(event);
        }
    }

    bool Start(const std::string& host, int port, const std::string& tokenHex, float radius)
    {
        Stop();
        (void)radius;
        WSADATA wsa{};
        WSAStartup(MAKEWORD(2, 2), &wsa);
        if (tokenHex.empty() || port <= 0) { SetError("сервер не поддерживает голос клиентов b3889"); return false; }
        if (!g_opus.Load()) { SetError("нет opus.dll в папке игры — голос недоступен"); return false; }
        g_token = _strtoui64(tokenHex.c_str(), nullptr, 16);

        addrinfo hints{}; hints.ai_family = AF_UNSPEC; hints.ai_socktype = SOCK_DGRAM; hints.ai_protocol = IPPROTO_UDP;
        addrinfo* list = nullptr;
        if (getaddrinfo(host.c_str(), std::to_string(port).c_str(), &hints, &list) != 0 || !list)
        {
            SetError("адрес голосового сервера не найден");
            return false;
        }
        memcpy(&g_server, list->ai_addr, list->ai_addrlen);
        g_serverLen = (int)list->ai_addrlen;
        g_sock = socket(list->ai_family, SOCK_DGRAM, IPPROTO_UDP);
        freeaddrinfo(list);
        if (g_sock == INVALID_SOCKET) { SetError("не удалось открыть UDP"); return false; }
        DWORD timeout = 200;
        setsockopt(g_sock, SOL_SOCKET, SO_RCVTIMEO, (const char*)&timeout, sizeof timeout);
        // Приём только от своего сервера.
        connect(g_sock, (sockaddr*)&g_server, g_serverLen);

        g_running = true;
        g_micOk = false;
        g_captureThread = std::thread(CaptureLoop);
        g_recvThread = std::thread(ReceiveLoop);
        g_playThread = std::thread(PlayLoop);
        Log("голос: запущен (UDP " + host + ":" + std::to_string(port) + ")");
        return true;
    }

    void Stop()
    {
        if (!g_running.exchange(false)) return;
        if (g_sock != INVALID_SOCKET) { closesocket(g_sock); g_sock = INVALID_SOCKET; }
        for (auto* t : { &g_captureThread, &g_recvThread, &g_playThread })
            if (t->joinable()) t->join();
        {
            std::lock_guard lock(g_mutex);
            for (auto& [id, s] : g_speakers) if (s.decoder) g_opus.decDestroy(s.decoder);
            g_speakers.clear();
        }
        g_talking = false;
        WSACleanup();
    }

    bool Running() { return g_running; }
    void SetTalking(bool talking) { g_talking = talking && g_running; }
    bool Talking() { return g_talking; }
    bool MicrophoneOk() { return g_micOk; }

    std::string LastError()
    {
        std::lock_guard lock(g_mutex);
        return g_error;
    }

    void SetGain(uint32_t id, float gain, float pan)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_speakers.find(id);
        if (it == g_speakers.end()) return;
        it->second.gain = std::clamp(gain, 0.f, 1.5f);
        it->second.pan = std::clamp(pan, -1.f, 1.f);
    }

    bool IsSpeaking(uint32_t id, uint32_t ms)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_speakers.find(id);
        return it != g_speakers.end() && it->second.lastPacket && GetTickCount64() - it->second.lastPacket < ms;
    }

    void Forget(uint32_t id)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_speakers.find(id);
        if (it == g_speakers.end()) return;
        if (it->second.decoder) g_opus.decDestroy(it->second.decoder);
        g_speakers.erase(it);
    }
}
