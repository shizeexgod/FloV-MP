#pragma once
// Проверка лицензии сервера на стороне игрока (LIC). Сервер при входе
// показывает подписанную сервером лицензий FloV:MP аренду: ключ, IP машины,
// срок. Клиент проверяет подпись вшитым открытым ключом, срок и то, что
// подключён именно к этой машине. Слитый ключ на чужом сервере, копия папки
// сервера на другой машине или вырезанная из сервера проверка лицензии
// такого подтверждения не дадут — официальный клиент туда не зайдёт.

#include <string>

namespace flov::license
{
    enum class Verdict { Ok, BadFormat, BadSignature, Expired, WrongMachine };

    struct Result
    {
        Verdict verdict = Verdict::BadFormat;
        std::string project;   // название проекта из лицензии
        std::string reason;    // для игрока и журнала
    };

    /// peerIp — адрес, к которому реально подключились (getpeername).
    /// nowUtc — секунды UTC; 0 — текущее время (параметр для тестов).
    Result Verify(const std::string& payloadB64, const std::string& signatureB64, const std::string& peerIp,
                  long long nowUtc = 0);

    /// Локальная сеть и сама машина: проверка не нужна (тесты, игра по LAN),
    /// а продавать игру по такому адресу нельзя.
    bool IsPrivateAddress(const std::string& ip);
}
