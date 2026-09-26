#include <windows.h>
#include <bcrypt.h>
#include <wincrypt.h>

#include <cstdio>
#include <ctime>
#include <vector>

#include "license.h"

#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "crypt32.lib")

namespace flov::license
{
    namespace
    {
        // Открытый ключ сервера лицензий FloV:MP (тот же, что в
        // server/src/FloVMP.Core/Licensing/LicenseFile.cs). Сменили ключ на VDS —
        // заменить в обоих местах.
        const char kAuthorityPem[] =
            "-----BEGIN PUBLIC KEY-----\n"
            "MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAst3jsCGnBI2txUgsxrru\n"
            "6JZoJJny2UQOyacxpNEiXP2Xe05OL6n8n1/LPDtcVwGXNJfT7PiYGrrYP5Gga4Wp\n"
            "jCcg6AXjNJXzz6hXcT9M/qyuB0RYVSaajYd8l8yUA7+IOPP7rimWJINKiJsXW/HU\n"
            "d7w0bScysKLI8kT7ojnkH2p6ubX5kBlkEz3P6wsz8rAnoQnEF4J6ac1roS9Xrevh\n"
            "mwymZZ3gR/+o55DMNIeZZHfjyLMeuaKbynHE4EJp8qT/6Apjm8JEU9RtyUtUEQZV\n"
            "vU+/Ip//eh5ysPTiWylicRz7ENVe/RBuWVJ1W71KzOyyTFiVHNWeSAG83i+s6nAE\n"
            "JKP4DwtcNMmkdzld5sXz89nnC7/3UWjK/Ee+coo6vedE+gRFcNpBpRtfbpcKxtD/\n"
            "5s7uA+Mea6HmIIdLwHZuzVGqlXEanTxWqXk68KGoAP/DfBI9NmTsEN6cPkgLTKlN\n"
            "4QvJu2opKshaG4gyMwiduX4W90ZRXbb+aVWo9pR69MTDAgMBAAE=\n"
            "-----END PUBLIC KEY-----\n";

        std::vector<BYTE> FromBase64(const std::string& text, DWORD flags)
        {
            DWORD size = 0;
            if (!CryptStringToBinaryA(text.c_str(), (DWORD)text.size(), flags, nullptr, &size, nullptr, nullptr)) return {};
            std::vector<BYTE> out(size);
            if (!CryptStringToBinaryA(text.c_str(), (DWORD)text.size(), flags, out.data(), &size, nullptr, nullptr)) return {};
            out.resize(size);
            return out;
        }

        bool VerifySignature(const std::vector<BYTE>& payload, const std::vector<BYTE>& signature)
        {
            const auto der = FromBase64(kAuthorityPem, CRYPT_STRING_BASE64HEADER);
            if (der.empty()) return false;
            CERT_PUBLIC_KEY_INFO* info = nullptr;
            DWORD infoSize = 0;
            if (!CryptDecodeObjectEx(X509_ASN_ENCODING, X509_PUBLIC_KEY_INFO, der.data(), (DWORD)der.size(),
                                     CRYPT_DECODE_ALLOC_FLAG, nullptr, &info, &infoSize))
                return false;
            BCRYPT_KEY_HANDLE key = nullptr;
            const BOOL imported = CryptImportPublicKeyInfoEx2(X509_ASN_ENCODING, info, 0, nullptr, &key);
            LocalFree(info);
            if (!imported || !key) return false;

            BYTE hash[32] = {};
            bool ok = BCryptHash(BCRYPT_SHA256_ALG_HANDLE, nullptr, 0, (PUCHAR)payload.data(), (ULONG)payload.size(), hash, sizeof hash) == 0;
            if (ok)
            {
                BCRYPT_PKCS1_PADDING_INFO pad{ BCRYPT_SHA256_ALGORITHM };
                ok = BCryptVerifySignature(key, &pad, hash, sizeof hash, (PUCHAR)signature.data(), (ULONG)signature.size(),
                                           BCRYPT_PAD_PKCS1) == 0;
            }
            BCryptDestroyKey(key);
            return ok;
        }

        /// Строковое поле плоского JSON сервера лицензий ("имя":"значение").
        std::string Field(const std::string& json, const char* name)
        {
            const std::string key = std::string("\"") + name + "\":\"";
            const size_t at = json.find(key);
            if (at == std::string::npos) return {};
            std::string out;
            for (size_t i = at + key.size(); i < json.size() && json[i] != '"'; ++i)
            {
                if (json[i] == '\\' && i + 1 < json.size()) { ++i; out += json[i] == 'n' ? '\n' : json[i]; continue; }
                out += json[i];
            }
            return out;
        }

        /// "2026-09-27T08:21:41.1234567Z" → секунды UTC; 0 — не разобрать.
        long long ParseUtc(const std::string& s)
        {
            std::tm t{};
            if (sscanf_s(s.c_str(), "%d-%d-%dT%d:%d:%d", &t.tm_year, &t.tm_mon, &t.tm_mday, &t.tm_hour, &t.tm_min, &t.tm_sec) != 6)
                return 0;
            t.tm_year -= 1900;
            t.tm_mon -= 1;
            return (long long)_mkgmtime(&t);
        }
    }

    bool IsPrivateAddress(const std::string& ip)
    {
        unsigned a = 0, b = 0, c = 0, d = 0;
        if (sscanf_s(ip.c_str(), "%u.%u.%u.%u", &a, &b, &c, &d) != 4) return ip == "::1";
        return a == 127 || a == 10 || (a == 172 && b >= 16 && b <= 31) || (a == 192 && b == 168) || (a == 100 && b >= 64 && b <= 127);
    }

    Result Verify(const std::string& payloadB64, const std::string& signatureB64, const std::string& peerIp, long long nowUtc)
    {
        Result r;
        const auto payload = FromBase64(payloadB64, CRYPT_STRING_BASE64);
        const auto signature = FromBase64(signatureB64, CRYPT_STRING_BASE64);
        if (payload.empty() || signature.empty())
        {
            r.reason = "сервер прислал повреждённое подтверждение лицензии";
            return r;
        }
        if (!VerifySignature(payload, signature))
        {
            r.verdict = Verdict::BadSignature;
            r.reason = "подтверждение лицензии подделано — подпись не от сервера лицензий FloV:MP";
            return r;
        }
        const std::string json(payload.begin(), payload.end());
        if (Field(json, "kind") != "attest")
        {
            r.reason = "сервер прислал не подтверждение для игроков";
            return r;
        }
        r.project = Field(json, "project");
        const long long until = ParseUtc(Field(json, "validUntil"));
        const long long now = nowUtc ? nowUtc : (long long)time(nullptr);
        if (until == 0 || until + 600 < now)   // 10 минут на расхождение часов
        {
            r.verdict = Verdict::Expired;
            r.reason = "лицензия сервера не подтверждена сервером лицензий FloV:MP (подтверждение устарело)";
            return r;
        }
        const std::string ip = Field(json, "ip");
        if (ip.empty())
        {
            r.reason = "в подтверждении лицензии нет адреса сервера";
            return r;
        }
        if (!peerIp.empty() && ip != peerIp && !IsPrivateAddress(peerIp))
        {
            r.verdict = Verdict::WrongMachine;
            r.reason = "лицензия выдана другой машине (" + ip + "), а сервер работает на " + peerIp;
            return r;
        }
        r.verdict = Verdict::Ok;
        return r;
    }
}
