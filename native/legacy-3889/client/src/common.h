#pragma once
// Общие утилиты клиента FloV:MP для GTA V Legacy 1.0.3889.0.

#include <windows.h>
#include <cstdint>
#include <string>
#include <vector>

namespace flov
{
    constexpr const char* kClientVersion = "1.0.0";
    constexpr const char* kProtocolVersion = "2";
    constexpr const char* kGameVersion = "1.0.3889.0";
    constexpr int kDefaultNativePort = 7798;

    /// %LOCALAPPDATA%\FloVMP — журнал, ключ, запрос на подключение.
    /// Папка игры (Program Files) обычно недоступна для записи.
    std::wstring DataDir();
    void Log(const std::string& text);
    /// Только в файл журнала (без копии в консоль F8).
    void WriteLog(const std::string& text);
    using LogHook = void (*)(const std::string&);
    void SetLogHook(LogHook hook);

    std::string ToUtf8(const std::wstring& text);
    std::wstring FromUtf8(const std::string& text);

    /// Версия файла GTA5.exe загруженного процесса ("1.0.3889.0").
    std::string GameVersion();

    /// Протокол FLOV/2: поля через табуляцию, \t \n \r \\ экранируются.
    std::string Escape(const std::string& value);
    std::vector<std::string> Parse(const std::string& line);
    std::string Format(const std::vector<std::string>& fields);

    std::string F(float v);
    float ToFloat(const std::string& s, float fallback = 0.f);
    int ToInt(const std::string& s, int fallback = 0);
    uint32_t ToUInt(const std::string& s, uint32_t fallback = 0);

    /// Хэш имени модели/оружия как у движка (joaat, без учёта регистра).
    uint32_t Joaat(const std::string& text);

    /// Ник по правилам сервера: 2..32 символа, без {}[]<> и управляющих.
    std::string SanitizeName(const std::string& name);
}
