#pragma once
// Протокол между клиентом (FloVMP.asi в GTA5.exe) и хостом браузеров
// (flovmp-cef.exe, пункт 26b). Общий для обеих сторон.
//
// Почему отдельный процесс. Chromium в процессе игры — это сотни мегабайт,
// свои потоки и свои перехваты; его падение уронило бы GTA. Хост живёт
// отдельно: падает — игра продолжает, браузеры поднимаются заново.
//
// Команды и события — строки через трубу, поля через табуляцию, как в FLOV/2.
// Кадры — через разделяемую память: хост пишет BGRA (альфа умножена на цвет,
// как отдаёт CEF), клиент копирует изменившуюся часть в текстуру DirectX.
//
// Клиент → хост:
//   NEW id url w h        создать браузер с собственным viewport
//   DEL id                закрыть
//   URL id url            перейти
//   EXEC id code          выполнить JS в странице
//   CALL id name json     событие в страницу: mp.events.add(name, …) в ней
//   SHOW id 0|1           видимость (скрытый не рисуется и не тратит время)
//   RATE id 1..60         максимум кадров страницы в секунду
//   RELOAD id 0|1         перезагрузить (1 — без кэша)
//   SIZE w h              разрешение игры/default для старого NEW без w/h
//   BOUNDS id w h         изменить viewport отдельного браузера
//   MOUSE id x y btn wh   мышь: координаты в пикселях, кнопки битами
//                         (1 левая, 2 правая, 4 средняя), колесо — шаги
//   LEAVE id              мышь ушла с браузера
//   KEY id msg wp lp mods клавиатура: WM_KEYDOWN/UP/CHAR как есть
//   FOCUS id 0|1
//   ROOT путь             папка скачанных client_packages (package://)
// Хост → клиент:
//   READY                 хост запущен
//   FRAME id имя w h      кадры браузера — в разделяемой памяти «имя»
//   DOM id url            страница загружена (browserDomReady)
//   FAIL id код url       не загрузилась (browserLoadingFailed)
//   TRIG id name json     mp.trigger(name, …) из страницы
//   LOG id уровень текст  console.* страницы
//   CURSOR id тип         форма курсора над страницей

#include <windows.h>

#include <cstdint>
#include <cstdio>
#include <string>
#include <vector>

namespace flov::browser_ipc
{
    constexpr uint32_t kMagic = 0x52424C46;   // «FLBR»
    constexpr int kMaxSide = 7680;           // 8K — с запасом, больше не бывает
    // Один browser — отдельная Chromium page + CPU BGRA + shared memory +
    // stable/staging buffers + GPU texture. Оба конца протокола обязаны
    // проверять эти лимиты: повреждённый/старый клиент не должен раздувать host.
    constexpr size_t kMaxBrowsers = 12;
    constexpr uint64_t kMaxTotalPixels = 12ull * 1920ull * 1080ull;

    /// Заголовок кадра в начале разделяемой памяти, за ним — пиксели.
    /// seq — счётчик записи: нечётный, пока хост пишет. Клиент читает кадр,
    /// только если seq чётный и не изменился за время копирования. dirty —
    /// объединение изменившихся областей с кадра, который клиент подтвердил
    /// (ack); клиент копирует только его.
    struct alignas(16) FrameHeader
    {
        uint32_t magic;
        uint32_t width;
        uint32_t height;
        uint32_t stride;
        volatile LONG64 seq;
        volatile LONG64 ack;
        int32_t dirtyX, dirtyY, dirtyW, dirtyH;
        uint32_t reserved[4];
    };

    /// Межпроцессные счётчики нельзя читать/писать обычным volatile: volatile
    /// не задаёт порядок памяти между ядрами. Interlocked даёт необходимый
    /// acquire/release барьер и на стороне CEF, и внутри GTA-клиента.
    inline LONG64 LoadCounter(volatile LONG64* value)
    {
        return InterlockedCompareExchange64(value, 0, 0);
    }

    inline void StoreCounter(volatile LONG64* value, LONG64 next)
    {
        InterlockedExchange64(value, next);
    }

    inline size_t FrameBytes(int w, int h) { return sizeof(FrameHeader) + (size_t)w * (size_t)h * 4; }

    // --- строки: табуляция между полями, \t \n \r \\ экранируются -------------------

    inline std::string Escape(const std::string& v)
    {
        std::string out;
        out.reserve(v.size());
        for (char c : v)
        {
            switch (c)
            {
            case '\\': out += "\\\\"; break;
            case '\t': out += "\\t"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            default: out += c;
            }
        }
        return out;
    }

    inline std::string Unescape(const std::string& v)
    {
        std::string out;
        out.reserve(v.size());
        for (size_t i = 0; i < v.size(); ++i)
        {
            if (v[i] == '\\' && i + 1 < v.size())
            {
                const char n = v[++i];
                out += n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n;
            }
            else out += v[i];
        }
        return out;
    }

    inline std::string Format(const std::vector<std::string>& fields)
    {
        std::string line;
        for (size_t i = 0; i < fields.size(); ++i)
        {
            if (i) line += '\t';
            line += Escape(fields[i]);
        }
        return line;
    }

    inline std::vector<std::string> Parse(const std::string& line)
    {
        std::vector<std::string> out;
        size_t start = 0;
        for (;;)
        {
            const size_t tab = line.find('\t', start);
            out.push_back(Unescape(line.substr(start, tab == std::string::npos ? std::string::npos : tab - start)));
            if (tab == std::string::npos) break;
            start = tab + 1;
        }
        return out;
    }

    /// Строка JS-литералом (для EXEC в страницу).
    inline std::string JsString(const std::string& v)
    {
        std::string out = "\"";
        for (unsigned char c : v)
        {
            switch (c)
            {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default:
                if (c < 0x20) { char b[8]; snprintf(b, sizeof b, "\\u%04x", c); out += b; }
                else out += (char)c;
            }
        }
        // U+2028/2029 — перевод строки для старых движков JS.
        size_t p;
        while ((p = out.find("\xE2\x80\xA8")) != std::string::npos) out.replace(p, 3, "\\u2028");
        while ((p = out.find("\xE2\x80\xA9")) != std::string::npos) out.replace(p, 3, "\\u2029");
        return out + "\"";
    }
}
