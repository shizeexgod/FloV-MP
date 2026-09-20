#include "flovmp_native_adapter.h"

#include <windows.h>
#include <bcrypt.h>

#include <array>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

#pragma comment(lib, "bcrypt.lib")

namespace {

constexpr wchar_t kId[] = L"flovmp-legacy-native-3889";
constexpr wchar_t kProfile[] = L"legacy-3889-epic";
constexpr wchar_t kGameVersion[] = L"1.0.3889.0";
constexpr wchar_t kBindingStatus[] = L"preflight-only-native-binding-pending";

constexpr std::array<std::uint8_t, 32> kGtaSha256 = {
    0x67, 0x7e, 0x4e, 0x35, 0x5c, 0xfb, 0xdb, 0x13,
    0x27, 0x3b, 0x1d, 0x99, 0x24, 0x07, 0xe3, 0xc2,
    0x61, 0xb3, 0xa1, 0x08, 0xdc, 0x4d, 0xd5, 0xc8,
    0xa0, 0xc4, 0xc1, 0xda, 0x65, 0x18, 0x02, 0xe5
};

constexpr std::array<std::uint8_t, 32> kUpdateSha256 = {
    0x91, 0x3a, 0x33, 0x53, 0x14, 0xb4, 0xc3, 0xc6,
    0x16, 0x39, 0x77, 0x82, 0xe4, 0x17, 0x5d, 0x6a,
    0x3c, 0xf2, 0x17, 0x21, 0x97, 0x50, 0xcb, 0x9b,
    0x45, 0x7b, 0x4a, 0x78, 0x1a, 0x73, 0xdd, 0xc0
};

constexpr std::array<std::uint8_t, 32> kUpdate2Sha256 = {
    0x8e, 0x20, 0x22, 0x69, 0x3d, 0x3b, 0xe6, 0xbf,
    0x39, 0x61, 0xbf, 0x59, 0x60, 0x45, 0xef, 0x27,
    0x62, 0xb3, 0x4f, 0x6a, 0xa4, 0x9d, 0x5a, 0xfb,
    0x80, 0x83, 0x65, 0x92, 0x96, 0xca, 0x13, 0x93
};

bool Sha256File(const std::filesystem::path& path,
                std::array<std::uint8_t, 32>& digest) {
    std::ifstream input(path, std::ios::binary);
    if (!input) return false;

    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    DWORD objectLength = 0;
    DWORD resultLength = 0;
    bool ok = false;

    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM,
                                    nullptr, 0) != 0) {
        return false;
    }
    if (BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH,
                          reinterpret_cast<PUCHAR>(&objectLength),
                          sizeof(objectLength), &resultLength, 0) != 0) {
        BCryptCloseAlgorithmProvider(algorithm, 0);
        return false;
    }

    std::vector<std::uint8_t> object(objectLength);
    if (BCryptCreateHash(algorithm, &hash, object.data(), objectLength,
                         nullptr, 0, 0) != 0) {
        BCryptCloseAlgorithmProvider(algorithm, 0);
        return false;
    }

    std::array<char, 1024 * 1024> buffer{};
    while (input) {
        input.read(buffer.data(), static_cast<std::streamsize>(buffer.size()));
        const auto count = input.gcount();
        if (count <= 0) break;
        if (BCryptHashData(hash,
                           reinterpret_cast<PUCHAR>(buffer.data()),
                           static_cast<ULONG>(count), 0) != 0) {
            BCryptDestroyHash(hash);
            BCryptCloseAlgorithmProvider(algorithm, 0);
            return false;
        }
    }

    ok = BCryptFinishHash(hash, digest.data(),
                          static_cast<ULONG>(digest.size()), 0) == 0;
    BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    return ok;
}

bool EqualsFile(const std::filesystem::path& path,
                const std::array<std::uint8_t, 32>& expected) {
    std::array<std::uint8_t, 32> actual{};
    return Sha256File(path, actual) && actual == expected;
}

} // namespace

FLOVMP_ADAPTER_API const FlovMpLegacy3889AdapterInfo*
FlovMpLegacy3889_GetInfo() {
    static const FlovMpLegacy3889AdapterInfo info{
        1, kId, kProfile, kGameVersion, kBindingStatus
    };
    return &info;
}

FLOVMP_ADAPTER_API int
FlovMpLegacy3889_ValidateGame(const wchar_t* gameDirectory) {
    if (gameDirectory == nullptr || *gameDirectory == L'\0') return 0;
    const std::filesystem::path root(gameDirectory);
    return EqualsFile(root / L"GTA5.exe", kGtaSha256) &&
                   EqualsFile(root / L"update" / L"update.rpf", kUpdateSha256) &&
                   EqualsFile(root / L"update" / L"update2.rpf", kUpdate2Sha256)
               ? 1
               : 0;
}

FLOVMP_ADAPTER_API int
FlovMpLegacy3889_IsRuntimeBound() {
    return 0;
}
