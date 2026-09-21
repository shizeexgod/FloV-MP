#include "identity.h"
#include "common.h"

#include <windows.h>
#include <bcrypt.h>
#include <wincrypt.h>
#include <iphlpapi.h>
#include <cstdio>

#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "iphlpapi.lib")

namespace flov
{
    namespace
    {
        std::vector<unsigned char> Sha256(const void* data, size_t size)
        {
            std::vector<unsigned char> out(32);
            BCryptHash(BCRYPT_SHA256_ALG_HANDLE, nullptr, 0, (PUCHAR)data, (ULONG)size, out.data(), 32);
            return out;
        }

        std::string Hex8(const std::vector<unsigned char>& hash)
        {
            char buf[17];
            for (int i = 0; i < 8; ++i) snprintf(buf + i * 2, 3, "%02X", hash[i]);
            return std::string(buf, 16);
        }

        std::vector<unsigned char> ReadFile(const std::wstring& path)
        {
            std::vector<unsigned char> data;
            FILE* f = nullptr;
            if (_wfopen_s(&f, path.c_str(), L"rb") != 0 || !f) return data;
            unsigned char buf[4096];
            size_t n;
            while ((n = fread(buf, 1, sizeof buf, f)) > 0) data.insert(data.end(), buf, buf + n);
            fclose(f);
            return data;
        }

        bool WriteFileAtomic(const std::wstring& path, const std::vector<unsigned char>& data)
        {
            const auto tmp = path + L".tmp";
            FILE* f = nullptr;
            if (_wfopen_s(&f, tmp.c_str(), L"wb") != 0 || !f) return false;
            const bool ok = fwrite(data.data(), 1, data.size(), f) == data.size();
            fclose(f);
            return ok && MoveFileExW(tmp.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING);
        }
    }

    std::string Base64(const std::vector<unsigned char>& data)
    {
        DWORD len = 0;
        CryptBinaryToStringA(data.data(), (DWORD)data.size(), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, nullptr, &len);
        std::string out(len, '\0');
        CryptBinaryToStringA(data.data(), (DWORD)data.size(), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, out.data(), &len);
        out.resize(len);
        return out;
    }

    std::vector<unsigned char> FromBase64(const std::string& text)
    {
        DWORD len = 0;
        if (!CryptStringToBinaryA(text.c_str(), (DWORD)text.size(), CRYPT_STRING_BASE64, nullptr, &len, nullptr, nullptr))
            return {};
        std::vector<unsigned char> out(len);
        CryptStringToBinaryA(text.c_str(), (DWORD)text.size(), CRYPT_STRING_BASE64, out.data(), &len, nullptr, nullptr);
        out.resize(len);
        return out;
    }

    Identity::~Identity()
    {
        if (_key) BCryptDestroyKey((BCRYPT_KEY_HANDLE)_key);
        if (_alg) BCryptCloseAlgorithmProvider((BCRYPT_ALG_HANDLE)_alg, 0);
    }

    bool Identity::Load(std::string& error)
    {
        BCRYPT_ALG_HANDLE alg = nullptr;
        if (BCryptOpenAlgorithmProvider(&alg, BCRYPT_ECDSA_P256_ALGORITHM, nullptr, 0) != 0)
        {
            error = "ECDSA P-256 недоступен в системе";
            return false;
        }
        _alg = alg;

        const auto path = DataDir() + L"\\identity.key";
        BCRYPT_KEY_HANDLE key = nullptr;
        auto sealed = ReadFile(path);
        if (!sealed.empty())
        {
            DATA_BLOB in{ (DWORD)sealed.size(), sealed.data() }, out{};
            if (CryptUnprotectData(&in, nullptr, nullptr, nullptr, nullptr, 0, &out))
            {
                if (BCryptImportKeyPair(alg, nullptr, BCRYPT_ECCPRIVATE_BLOB, &key, out.pbData, out.cbData, 0) != 0)
                    key = nullptr;
                SecureZeroMemory(out.pbData, out.cbData);
                LocalFree(out.pbData);
            }
            if (!key) Log("identity: ключ не читается (другой пользователь Windows?) — создаю новый");
        }

        if (!key)
        {
            if (BCryptGenerateKeyPair(alg, &key, 256, 0) != 0 || BCryptFinalizeKeyPair(key, 0) != 0)
            {
                error = "не удалось создать ключ игрока";
                return false;
            }
            ULONG size = 0;
            BCryptExportKey(key, nullptr, BCRYPT_ECCPRIVATE_BLOB, nullptr, 0, &size, 0);
            std::vector<unsigned char> blob(size);
            BCryptExportKey(key, nullptr, BCRYPT_ECCPRIVATE_BLOB, blob.data(), size, &size, 0);
            DATA_BLOB in{ size, blob.data() }, out{};
            if (CryptProtectData(&in, L"FloV:MP identity", nullptr, nullptr, nullptr, 0, &out))
            {
                std::vector<unsigned char> sealedNew(out.pbData, out.pbData + out.cbData);
                LocalFree(out.pbData);
                if (!WriteFileAtomic(path, sealedNew))
                    Log("identity: не удалось сохранить ключ — ID игрока сменится при следующем запуске");
            }
            SecureZeroMemory(blob.data(), blob.size());
        }
        _key = key;

        ULONG size = 0;
        BCryptExportKey(key, nullptr, BCRYPT_ECCPUBLIC_BLOB, nullptr, 0, &size, 0);
        std::vector<unsigned char> pub(size);
        BCryptExportKey(key, nullptr, BCRYPT_ECCPUBLIC_BLOB, pub.data(), size, &size, 0);
        if (size != sizeof(BCRYPT_ECCKEY_BLOB) + 64)
        {
            error = "неожиданный формат открытого ключа";
            return false;
        }
        _public.assign(pub.begin() + sizeof(BCRYPT_ECCKEY_BLOB), pub.end());
        return true;
    }

    std::string Identity::PublicKeyB64() const { return Base64(_public); }

    std::string Identity::SignB64(const std::vector<unsigned char>& message) const
    {
        const auto hash = Sha256(message.data(), message.size());
        std::vector<unsigned char> sig(64);
        ULONG len = 0;
        if (BCryptSignHash((BCRYPT_KEY_HANDLE)_key, nullptr, (PUCHAR)hash.data(), 32, sig.data(), 64, &len, 0) != 0)
            return {};
        sig.resize(len);
        return Base64(sig);
    }

    std::string HardwareIdHex()
    {
        std::wstring material;
        HKEY key;
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Cryptography", 0,
                          KEY_READ | KEY_WOW64_64KEY, &key) == ERROR_SUCCESS)
        {
            wchar_t guid[128]{};
            DWORD size = sizeof guid;
            if (RegQueryValueExW(key, L"MachineGuid", nullptr, nullptr, (LPBYTE)guid, &size) == ERROR_SUCCESS)
                material += guid;
            RegCloseKey(key);
        }
        DWORD serial = 0;
        GetVolumeInformationW(L"C:\\", nullptr, 0, &serial, nullptr, nullptr, nullptr, 0);
        material += L"|" + std::to_wstring(serial);
        return Hex8(Sha256(material.data(), material.size() * sizeof(wchar_t)));
    }

    std::string MacHashHex()
    {
        ULONG size = 0;
        GetAdaptersInfo(nullptr, &size);
        if (!size) return "0000000000000000";
        std::vector<unsigned char> buffer(size);
        auto* info = reinterpret_cast<IP_ADAPTER_INFO*>(buffer.data());
        if (GetAdaptersInfo(info, &size) != NO_ERROR) return "0000000000000000";
        for (auto* a = info; a; a = a->Next)
        {
            if (a->AddressLength == 6 && (a->Type == MIB_IF_TYPE_ETHERNET || a->Type == 71 /* Wi-Fi */))
                return Hex8(Sha256(a->Address, a->AddressLength));
        }
        return "0000000000000000";
    }
}
