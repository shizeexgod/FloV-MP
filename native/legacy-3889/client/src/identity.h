#pragma once
#include <string>
#include <vector>

namespace flov
{
    /// Ключ игрока (ECDSA P-256). Создаётся один раз на компьютере и
    /// хранится в %LOCALAPPDATA%\FloVMP\identity.key под DPAPI: скопированный
    /// на другую машину или в другой профиль Windows файл не расшифруется.
    class Identity
    {
    public:
        ~Identity();
        bool Load(std::string& error);
        /// 64 байта X||Y в base64 — как ждёт сервер.
        std::string PublicKeyB64() const;
        /// Подпись r||s (64 байта) в base64 над message.
        std::string SignB64(const std::vector<unsigned char>& message) const;
        const std::vector<unsigned char>& PublicKey() const { return _public; }

    private:
        void* _alg = nullptr;
        void* _key = nullptr;
        std::vector<unsigned char> _public;
    };

    std::string Base64(const std::vector<unsigned char>& data);
    std::vector<unsigned char> FromBase64(const std::string& text);

    /// Отпечаток компьютера (для банов по железу): хэш MachineGuid и серийника диска.
    std::string HardwareIdHex();
    /// Хэш MAC-адреса первого физического адаптера.
    std::string MacHashHex();
}
