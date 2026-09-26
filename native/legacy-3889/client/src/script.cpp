#include "script.h"
#include "browser.h"
#include "common.h"
#include "http.h"
#include "ui.h"

#include <windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <deque>
#include <fstream>
#include <map>
#include <mutex>
#include <sstream>
#include <thread>

extern "C" {
#include "quickjs.h"
}

namespace flov::script
{
    namespace
    {
        // --- таблица нативов (tools/gen_js_natives.py) ------------------------------
        struct NativeInfo
        {
            const char* ns;
            const char* name;
            uint64_t hash;
            const char* args;   // i b f s — вход; I F V — выходные указатели
            char ret;           // v i b f s V
            const char* outs;   // имена выходных параметров через запятую
        };
        const NativeInfo kNatives[] = {
#include "js_natives.inc"
        };

        constexpr uint64_t kPlayerPedId = 0xD80958FC74E988A6ull;
        constexpr uint64_t kGetEntityCoords = 0x3FEF770D40960D5Aull;
        constexpr uint64_t kGetEntityHeading = 0xE83D4F9BA2A38914ull;

        // --- ограничения --------------------------------------------------------------
        constexpr size_t kMemoryLimit = 256u * 1024u * 1024u;
        // Скрипт работает в волокне ScriptHookV, у которого свой, небольшой стек:
        // глубокую рекурсию движок должен остановить сам, а не уронить игру.
        constexpr size_t kStackLimit = 256u * 1024u;
        constexpr ULONGLONG kStartBudgetMs = 3000;   // первый запуск index.js
        constexpr ULONGLONG kCallBudgetMs = 100;     // кадр, событие, таймер
        constexpr int kMaxOutgoingPerSecond = 100;
        constexpr size_t kMaxEventJson = 3800;
        constexpr size_t kMaxQueuedEvents = 512;

        // --- состояние ------------------------------------------------------------------
        NativeBackend g_backend;
        JSRuntime* g_rt = nullptr;
        JSContext* g_ctx = nullptr;
        ULONGLONG g_deadline = 0;
        bool g_interrupted = false;
        std::wstring g_packageDir;          // откуда require читает файлы
        std::string g_runningDigest;
        std::deque<std::pair<std::string, std::string>> g_inbox;   // события до запуска и между кадрами
        std::vector<std::pair<std::string, std::string>> g_outbox;
        int g_outCount = 0;
        ULONGLONG g_outWindow = 0;
        int g_localId = -1;
        std::string g_localName;
        bool g_cursor = false;
        bool g_cursorFreeze = false;        // mp.gui.cursor.show(freeze, …): управление персонажем выключено

        // Скачивание — в своём потоке, результат забирает игровой поток.
        std::mutex g_dl;
        std::atomic<int> g_generation{ 0 };
        bool g_pending = false;
        bool g_readyToStart = false;
        std::wstring g_readyDir;
        std::string g_readyDigest;
        long long g_dlDone = 0, g_dlTotal = 0;

        void Say(int level, const std::string& text)
        {
            const char* tag = level >= 2 ? "ERR" : level == 1 ? "WARN" : "JS";
            ui::ConsoleLog(tag, "[JS] " + text);
            WriteLog("[JS] " + text);
        }

        // --- SHA-256 и пути ----------------------------------------------------------------
        std::string HexSha256File(const std::wstring& path)
        {
            BCRYPT_ALG_HANDLE alg = nullptr;
            BCRYPT_HASH_HANDLE hash = nullptr;
            std::string out;
            if (BCryptOpenAlgorithmProvider(&alg, BCRYPT_SHA256_ALGORITHM, nullptr, 0) != 0) return out;
            if (BCryptCreateHash(alg, &hash, nullptr, 0, nullptr, 0, 0) == 0)
            {
                FILE* f = nullptr;
                if (_wfopen_s(&f, path.c_str(), L"rb") == 0 && f)
                {
                    std::vector<unsigned char> buf(1 << 16);
                    size_t n;
                    while ((n = fread(buf.data(), 1, buf.size(), f)) > 0) BCryptHashData(hash, buf.data(), (ULONG)n, 0);
                    fclose(f);
                    unsigned char digest[32];
                    if (BCryptFinishHash(hash, digest, sizeof digest, 0) == 0)
                    {
                        static const char* hex = "0123456789abcdef";
                        for (unsigned char b : digest) { out += hex[b >> 4]; out += hex[b & 15]; }
                    }
                }
                BCryptDestroyHash(hash);
            }
            BCryptCloseAlgorithmProvider(alg, 0);
            return out;
        }

        /// Те же правила, что у сервера (ModManifest.ValidPath с набором клиентских пакетов).
        bool ValidPackagePath(const std::string& path)
        {
            static const char* kExt[] = { ".js", ".mjs", ".json", ".map", ".html", ".htm", ".css", ".txt", ".md",
                                          ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico",
                                          ".woff", ".woff2", ".ttf", ".otf", ".mp3", ".ogg", ".wav", ".webm", ".mp4" };
            if (path.empty() || path.size() > 240 || path.front() == '/' || path.back() == '/') return false;
            for (unsigned char c : path)
                if (c < 0x20 || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|') return false;
            size_t start = 0;
            while (start <= path.size())
            {
                const size_t slash = path.find('/', start);
                const std::string part = path.substr(start, slash == std::string::npos ? std::string::npos : slash - start);
                if (part.empty() || part == "." || part == ".." || part.back() == '.' || part.back() == ' ') return false;
                if (slash == std::string::npos) break;
                start = slash + 1;
            }
            const size_t dot = path.find_last_of('.');
            if (dot == std::string::npos || path.find('/', dot) != std::string::npos) return false;
            std::string ext = path.substr(dot);
            for (auto& c : ext) c = (char)tolower((unsigned char)c);
            return std::any_of(std::begin(kExt), std::end(kExt), [&](const char* e) { return ext == e; });
        }

        std::wstring LocalPath(const std::wstring& dir, const std::string& rel)
        {
            std::wstring w = FromUtf8(rel);
            std::replace(w.begin(), w.end(), L'/', L'\\');
            return dir + L"\\" + w;
        }

        bool ReadFileUtf8(const std::wstring& path, std::string& out)
        {
            FILE* f = nullptr;
            if (_wfopen_s(&f, path.c_str(), L"rb") != 0 || !f) return false;
            std::string data;
            char buf[1 << 15];
            size_t n;
            while ((n = fread(buf, 1, sizeof buf, f)) > 0) data.append(buf, n);
            fclose(f);
            // BOM редакторов Windows движку не нужен.
            if (data.size() >= 3 && (unsigned char)data[0] == 0xEF && (unsigned char)data[1] == 0xBB && (unsigned char)data[2] == 0xBF)
                data.erase(0, 3);
            out.swap(data);
            return true;
        }

        void CreateDirs(const std::wstring& path)
        {
            for (size_t i = 3; i < path.size(); ++i)
                if (path[i] == L'\\') CreateDirectoryW(path.substr(0, i).c_str(), nullptr);
        }

        // --- исключения JS ---------------------------------------------------------------
        std::string DescribeException(JSContext* ctx)
        {
            JSValue ex = JS_GetException(ctx);
            std::string text;
            if (const char* s = JS_ToCString(ctx, ex)) { text = s; JS_FreeCString(ctx, s); }
            if (JS_IsObject(ex))
            {
                JSValue stack = JS_GetPropertyStr(ctx, ex, "stack");
                if (!JS_IsUndefined(stack))
                    if (const char* s = JS_ToCString(ctx, stack)) { if (*s) { text += "\n"; text += s; } JS_FreeCString(ctx, s); }
                JS_FreeValue(ctx, stack);
            }
            JS_FreeValue(ctx, ex);
            if (g_interrupted)
            {
                text = "скрипт работал дольше допустимого и был прерван, чтобы не повесить игру. " + text;
                g_interrupted = false;
            }
            return text;
        }

        int Interrupt(JSRuntime*, void*)
        {
            if (g_deadline && GetTickCount64() > g_deadline) { g_interrupted = true; return 1; }
            return 0;
        }

        void RunJobs()
        {
            JSContext* jobCtx = nullptr;
            for (int i = 0; i < 1000; ++i)
            {
                const int r = JS_ExecutePendingJob(g_rt, &jobCtx);
                if (r == 0) break;
                if (r < 0) { Say(2, "обещание: " + DescribeException(jobCtx)); }
            }
        }

        // --- вызов нативов ------------------------------------------------------------------
        void Push(uint64_t v) { g_backend.push(v); }

        uint64_t FloatBits(double d)
        {
            const float f = (float)d;
            uint32_t bits;
            memcpy(&bits, &f, 4);
            return bits;
        }

        float BitsFloat(uint64_t v)
        {
            const uint32_t bits = (uint32_t)v;
            float f;
            memcpy(&f, &bits, 4);
            return f;
        }

        JSValue Vec3(JSContext* ctx, const uint64_t* p)
        {
            JSValue o = JS_NewObject(ctx);
            JS_SetPropertyStr(ctx, o, "x", JS_NewFloat64(ctx, BitsFloat(p[0])));
            JS_SetPropertyStr(ctx, o, "y", JS_NewFloat64(ctx, BitsFloat(p[1])));
            JS_SetPropertyStr(ctx, o, "z", JS_NewFloat64(ctx, BitsFloat(p[2])));
            return o;
        }

        JSValue Result(JSContext* ctx, char kind, const uint64_t* r)
        {
            if (!r) return JS_UNDEFINED;
            switch (kind)
            {
            case 'v': return JS_UNDEFINED;
            case 'i': return JS_NewInt32(ctx, (int32_t)r[0]);
            case 'b': return JS_NewBool(ctx, (uint32_t)r[0] != 0);
            case 'f': return JS_NewFloat64(ctx, BitsFloat(r[0]));
            case 's': { const char* s = reinterpret_cast<const char*>(r[0]); return s ? JS_NewString(ctx, s) : JS_NULL; }
            case 'V': return Vec3(ctx, r);
            }
            return JS_UNDEFINED;
        }

        /// mp.game.<пространство>.<функция>(…): magic — номер строки таблицы.
        JSValue CallNative(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv, int magic, JSValueConst*)
        {
            if (!g_backend.begin) return JS_ThrowInternalError(ctx, "нативы недоступны");
            const NativeInfo& n = kNatives[magic];
            const size_t codes = strlen(n.args);
            std::vector<std::string> strings;
            strings.reserve(codes);
            uint64_t outs[16][3] = {};
            int outCount = 0;
            g_backend.begin(n.hash);
            int ai = 0;
            for (size_t k = 0; k < codes; ++k)
            {
                const char c = n.args[k];
                if (c == 'I' || c == 'F' || c == 'V')
                {
                    if (outCount >= 16) return JS_ThrowInternalError(ctx, "слишком много выходных параметров");
                    Push(reinterpret_cast<uint64_t>(&outs[outCount++][0]));
                    continue;
                }
                JSValueConst a = ai < argc ? argv[ai] : JS_UNDEFINED;
                ++ai;
                if (c == 'f') { double d = 0; JS_ToFloat64(ctx, &d, a); Push(FloatBits(d)); }
                else if (c == 'b') { Push(JS_ToBool(ctx, a) == 1 ? 1 : 0); }
                else if (c == 's')
                {
                    if (JS_IsNull(a) || JS_IsUndefined(a)) { Push(0); continue; }
                    const char* s = JS_ToCString(ctx, a);
                    strings.emplace_back(s ? s : "");
                    if (s) JS_FreeCString(ctx, s);
                    Push(reinterpret_cast<uint64_t>(strings.back().c_str()));
                }
                else
                {
                    int64_t v = 0;
                    if (JS_IsBool(a)) v = JS_ToBool(ctx, a) == 1 ? 1 : 0;
                    else JS_ToInt64(ctx, &v, a);
                    Push((uint64_t)v);
                }
            }
            const uint64_t* r = g_backend.call();
            JSValue result = Result(ctx, n.ret, r);
            if (outCount == 0) return result;

            // Выходные параметры — объектом, как в RAGE:MP: { result, groundZ }.
            JSValue o = JS_NewObject(ctx);
            JS_SetPropertyStr(ctx, o, "result", result);
            std::string names = n.outs;
            int k = 0;
            size_t pos = 0;
            for (size_t i = 0; i < codes && k < outCount; ++i)
            {
                const char c = n.args[i];
                if (c != 'I' && c != 'F' && c != 'V') continue;
                const size_t comma = names.find(',', pos);
                const std::string name = names.substr(pos, comma == std::string::npos ? std::string::npos : comma - pos);
                pos = comma == std::string::npos ? names.size() : comma + 1;
                JSValue v = c == 'V' ? Vec3(ctx, outs[k]) : c == 'F' ? JS_NewFloat64(ctx, BitsFloat(outs[k][0]))
                                                               : JS_NewInt32(ctx, (int32_t)outs[k][0]);
                JS_SetPropertyStr(ctx, o, name.empty() ? ("out" + std::to_string(k)).c_str() : name.c_str(), v);
                ++k;
            }
            return o;
        }

        /// mp.game.invoke('0xHASH', …) и варианты: magic — тип результата.
        /// Целые числа идут как int, дробные — как float, {float: n} — явно float.
        JSValue InvokeHash(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv, int magic)
        {
            if (!g_backend.begin) return JS_ThrowInternalError(ctx, "нативы недоступны");
            if (argc < 1) return JS_ThrowTypeError(ctx, "mp.game.invoke: нужен хэш натива строкой '0x…'");
            uint64_t hash = 0;
            if (JS_IsString(argv[0]))
            {
                const char* s = JS_ToCString(ctx, argv[0]);
                hash = s ? _strtoui64(s, nullptr, 0) : 0;
                if (s) JS_FreeCString(ctx, s);
            }
            else { double d = 0; JS_ToFloat64(ctx, &d, argv[0]); hash = (uint64_t)d; }
            if (!hash) return JS_ThrowTypeError(ctx, "mp.game.invoke: неверный хэш");
            std::vector<std::string> strings;
            strings.reserve(argc);
            g_backend.begin(hash);
            for (int i = 1; i < argc; ++i)
            {
                JSValueConst a = argv[i];
                if (JS_IsString(a))
                {
                    const char* s = JS_ToCString(ctx, a);
                    strings.emplace_back(s ? s : "");
                    if (s) JS_FreeCString(ctx, s);
                    Push(reinterpret_cast<uint64_t>(strings.back().c_str()));
                }
                else if (JS_IsBool(a)) Push(JS_ToBool(ctx, a) == 1 ? 1 : 0);
                else if (JS_IsNumber(a))
                {
                    double d = 0;
                    JS_ToFloat64(ctx, &d, a);
                    if (d == std::floor(d) && std::fabs(d) < 9.2e18) Push((uint64_t)(int64_t)d);
                    else Push(FloatBits(d));
                }
                else if (JS_IsObject(a))
                {
                    JSValue f = JS_GetPropertyStr(ctx, a, "float");
                    double d = 0;
                    JS_ToFloat64(ctx, &d, f);
                    JS_FreeValue(ctx, f);
                    Push(FloatBits(d));
                }
                else Push(0);
            }
            const uint64_t* r = g_backend.call();
            static const char kinds[] = { 'i', 'f', 's', 'V', 'b' };
            return Result(ctx, kinds[std::clamp(magic, 0, 4)], r);
        }

        uint64_t* CallSimple(uint64_t hash, std::initializer_list<uint64_t> args)
        {
            if (!g_backend.begin) return nullptr;
            g_backend.begin(hash);
            for (uint64_t a : args) g_backend.push(a);
            return g_backend.call();
        }

        // --- мост __flov ------------------------------------------------------------------------
        JSValue F_read(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc < 1 || g_packageDir.empty()) return JS_NULL;
            const char* p = JS_ToCString(ctx, argv[0]);
            std::string rel = p ? p : "";
            if (p) JS_FreeCString(ctx, p);
            if (!ValidPackagePath(rel)) return JS_NULL;
            std::string data;
            if (!ReadFileUtf8(LocalPath(g_packageDir, rel), data)) return JS_NULL;
            return JS_NewStringLen(ctx, data.data(), data.size());
        }

        JSValue F_compile(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc < 2) return JS_ThrowTypeError(ctx, "compile: нужен код и имя файла");
            const char* code = JS_ToCString(ctx, argv[0]);
            const char* file = JS_ToCString(ctx, argv[1]);
            // Обёртка на той же строке, что и первая строка модуля: номера строк
            // в ошибках совпадают с файлом.
            std::string src = "(function (module, exports, require, __filename, __dirname) {";
            src += code ? code : "";
            src += "\n})";
            JSValue fn = JS_Eval(ctx, src.c_str(), src.size(), file ? file : "<module>", JS_EVAL_TYPE_GLOBAL);
            if (code) JS_FreeCString(ctx, code);
            if (file) JS_FreeCString(ctx, file);
            return fn;
        }

        JSValue F_send(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc < 2) return JS_UNDEFINED;
            const char* n = JS_ToCString(ctx, argv[0]);
            const char* j = JS_ToCString(ctx, argv[1]);
            std::string name = n ? n : "", json = j ? j : "[]";
            if (n) JS_FreeCString(ctx, n);
            if (j) JS_FreeCString(ctx, j);
            const bool okName = !name.empty() && name.size() <= 64 &&
                std::all_of(name.begin(), name.end(), [](unsigned char c) { return isalnum(c) || c == '_' || c == ':' || c == '.' || c == '-'; });
            if (!okName) return JS_ThrowTypeError(ctx, "mp.events.callRemote: имя события — буквы, цифры, _:.- , до 64 символов");
            if (json.size() > kMaxEventJson) return JS_ThrowRangeError(ctx, "mp.events.callRemote: аргументы длиннее %d символов", (int)kMaxEventJson);
            const ULONGLONG now = GetTickCount64();
            if (now - g_outWindow >= 1000) { g_outWindow = now; g_outCount = 0; }
            if (++g_outCount > kMaxOutgoingPerSecond)
                return JS_ThrowRangeError(ctx, "mp.events.callRemote: больше %d событий в секунду", kMaxOutgoingPerSecond);
            g_outbox.emplace_back(std::move(name), std::move(json));
            return JS_UNDEFINED;
        }

        JSValue F_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            int level = 0;
            if (argc > 0) JS_ToInt32(ctx, &level, argv[0]);
            const char* t = argc > 1 ? JS_ToCString(ctx, argv[1]) : nullptr;
            Say(level, t ? t : "");
            if (t) JS_FreeCString(ctx, t);
            return JS_UNDEFINED;
        }

        JSValue F_error(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            std::string where, text;
            if (argc > 0) if (const char* s = JS_ToCString(ctx, argv[0])) { where = s; JS_FreeCString(ctx, s); }
            if (argc > 1)
            {
                if (const char* s = JS_ToCString(ctx, argv[1])) { text = s; JS_FreeCString(ctx, s); }
                if (JS_IsObject(argv[1]))
                {
                    JSValue st = JS_GetPropertyStr(ctx, argv[1], "stack");
                    if (const char* s = JS_ToCString(ctx, st)) { if (*s && strcmp(s, "undefined")) { text += "\n"; text += s; } JS_FreeCString(ctx, s); }
                    JS_FreeValue(ctx, st);
                }
            }
            Say(2, where + ": " + text);
            return JS_UNDEFINED;
        }

        JSValue F_now(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewFloat64(ctx, (double)GetTickCount64()); }

        JSValue F_keyDown(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            int vk = 0;
            if (argc > 0) JS_ToInt32(ctx, &vk, argv[0]);
            if (vk <= 0 || vk > 254) return JS_FALSE;
            // Только когда окно игры активно: иначе скрипт ловил бы клавиши,
            // нажатые игроком в другой программе.
            const bool focused = GetForegroundWindow() && GetCurrentProcessId() ==
                [] { DWORD pid = 0; GetWindowThreadProcessId(GetForegroundWindow(), &pid); return pid; }();
            return JS_NewBool(ctx, focused && (GetAsyncKeyState(vk) & 0x8000) != 0);
        }

        JSValue F_chatPush(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 0) if (const char* s = JS_ToCString(ctx, argv[0])) { ui::AddChat(std::string(s).substr(0, 1024)); JS_FreeCString(ctx, s); }
            return JS_UNDEFINED;
        }

        JSValue F_chatShow(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            ui::SetChatEnabled(argc > 0 && JS_ToBool(ctx, argv[0]) == 1);
            return JS_UNDEFINED;
        }

        JSValue F_cursor(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            g_cursor = argc > 0 && JS_ToBool(ctx, argv[0]) == 1;
            g_cursorFreeze = g_cursor && argc > 1 && JS_ToBool(ctx, argv[1]) == 1;
            return JS_UNDEFINED;
        }

        JSValue F_cursorVisible(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewBool(ctx, g_cursor); }

        // --- браузеры (пункт 26b) ---
        std::string Str(JSContext* ctx, JSValueConst v, size_t limit)
        {
            const char* s = JS_ToCString(ctx, v);
            if (!s) return {};
            std::string out(s);
            JS_FreeCString(ctx, s);
            if (out.size() > limit) out.resize(limit);
            return out;
        }

        int Int(JSContext* ctx, JSValueConst v)
        {
            int32_t i = 0;
            JS_ToInt32(ctx, &i, v);
            return i;
        }

        JSValue F_brNew(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc < 1) return JS_NewInt32(ctx, 0);
            return JS_NewInt32(ctx, browser::Create(Str(ctx, argv[0], 4096)));
        }
        JSValue F_brDel(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 0) browser::Destroy(Int(ctx, argv[0]));
            return JS_UNDEFINED;
        }
        JSValue F_brUrl(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::SetUrl(Int(ctx, argv[0]), Str(ctx, argv[1], 4096));
            return JS_UNDEFINED;
        }
        JSValue F_brExec(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::Execute(Int(ctx, argv[0]), Str(ctx, argv[1], 512 * 1024));
            return JS_UNDEFINED;
        }
        JSValue F_brCall(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 2) browser::Call(Int(ctx, argv[0]), Str(ctx, argv[1], 128), Str(ctx, argv[2], 512 * 1024));
            return JS_UNDEFINED;
        }
        JSValue F_brShow(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::Show(Int(ctx, argv[0]), JS_ToBool(ctx, argv[1]) == 1);
            return JS_UNDEFINED;
        }
        JSValue F_brInput(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::SetInputEnabled(Int(ctx, argv[0]), JS_ToBool(ctx, argv[1]) == 1);
            return JS_UNDEFINED;
        }
        JSValue F_brOrder(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::SetOrder(Int(ctx, argv[0]), Int(ctx, argv[1]));
            return JS_UNDEFINED;
        }
        JSValue F_brRate(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::SetFrameRate(Int(ctx, argv[0]), Int(ctx, argv[1]));
            return JS_UNDEFINED;
        }
        JSValue F_brReload(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::Reload(Int(ctx, argv[0]), JS_ToBool(ctx, argv[1]) == 1);
            return JS_UNDEFINED;
        }
        JSValue F_brBounds(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 4) browser::SetBounds(Int(ctx, argv[0]), Int(ctx, argv[1]), Int(ctx, argv[2]),
                                              Int(ctx, argv[3]), Int(ctx, argv[4]));
            return JS_UNDEFINED;
        }
        JSValue F_brFocus(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv)
        {
            if (argc > 1) browser::Focus(Int(ctx, argv[0]), JS_ToBool(ctx, argv[1]) == 1);
            return JS_UNDEFINED;
        }
        JSValue F_brFocused(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewInt32(ctx, browser::Focused()); }
        JSValue F_brAvailable(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewBool(ctx, browser::Available()); }
        JSValue F_brMax(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewInt32(ctx, browser::MaxCount()); }
        JSValue F_brStats(JSContext* ctx, JSValueConst, int, JSValueConst*)
        {
            const auto s = browser::GetStats();
            const std::string json = "{\"count\":" + std::to_string(s.count) +
                ",\"visible\":" + std::to_string(s.visible) +
                ",\"maxBrowsers\":" + std::to_string(s.maxBrowsers) +
                ",\"screenWidth\":" + std::to_string(s.screenWidth) +
                ",\"screenHeight\":" + std::to_string(s.screenHeight) +
                ",\"maxRenderWidth\":" + std::to_string(s.maxRenderWidth) +
                ",\"maxRenderHeight\":" + std::to_string(s.maxRenderHeight) +
                ",\"recoveryFailed\":" + (s.recoveryFailed ? "true" : "false") +
                ",\"crashesInWindow\":" + std::to_string(s.crashesInWindow) +
                ",\"pixels\":" + std::to_string(s.pixels) +
                ",\"estimatedBytes\":" + std::to_string(s.estimatedBytes) +
                ",\"uploadedFrames\":" + std::to_string(s.uploadedFrames) +
                ",\"droppedFrames\":" + std::to_string(s.droppedFrames) +
                ",\"uploadMicros\":" + std::to_string(s.uploadMicros) + "}";
            return JS_NewStringLen(ctx, json.data(), json.size());
        }

        JSValue F_localHandle(JSContext* ctx, JSValueConst, int, JSValueConst*)
        {
            const uint64_t* r = CallSimple(kPlayerPedId, {});
            return JS_NewInt32(ctx, r ? (int32_t)r[0] : 0);
        }

        JSValue F_localPos(JSContext* ctx, JSValueConst, int, JSValueConst*)
        {
            const uint64_t* ped = CallSimple(kPlayerPedId, {});
            if (!ped) return JS_NULL;
            const uint64_t handle = (uint32_t)ped[0];
            const uint64_t* r = CallSimple(kGetEntityCoords, { handle, 1 });
            return r ? Vec3(ctx, r) : JS_NULL;
        }

        JSValue F_localHeading(JSContext* ctx, JSValueConst, int, JSValueConst*)
        {
            const uint64_t* ped = CallSimple(kPlayerPedId, {});
            if (!ped) return JS_NewFloat64(ctx, 0);
            const uint64_t handle = (uint32_t)ped[0];
            const uint64_t* r = CallSimple(kGetEntityHeading, { handle });
            return JS_NewFloat64(ctx, r ? BitsFloat(r[0]) : 0.f);
        }

        JSValue F_localId(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewInt32(ctx, g_localId); }
        JSValue F_localName(JSContext* ctx, JSValueConst, int, JSValueConst*) { return JS_NewString(ctx, g_localName.c_str()); }

        void AddFn(JSContext* ctx, JSValue obj, const char* name, JSCFunction* fn, int len)
        {
            JS_SetPropertyStr(ctx, obj, name, JS_NewCFunction(ctx, fn, name, len));
        }

        // --- пролог на JS: события, таймеры, клавиши, require, mp.* -----------------------
        const char* kPrelude = R"JS(
(function () {
'use strict';
const F = globalThis.__flov;
const mp = globalThis.mp;

// ---- события: как mp.events в RAGE:MP ----
const listeners = new Map();
function add(name, fn) {
    if (name && typeof name === 'object') { for (const k of Object.keys(name)) add(k, name[k]); return; }
    if (typeof fn !== 'function') throw new TypeError('mp.events.add: обработчик должен быть функцией');
    let list = listeners.get(name);
    if (!list) { list = []; listeners.set(name, list); }
    list.push(fn);
}
function remove(name, fn) {
    if (Array.isArray(name)) { for (const n of name) remove(n, fn); return; }
    if (fn === undefined) { listeners.delete(name); return; }
    const list = listeners.get(name);
    if (!list) return;
    const i = list.indexOf(fn);
    if (i >= 0) list.splice(i, 1);
}
function dispatch(name, args) {
    const list = listeners.get(name);
    if (!list) return;
    for (const fn of list.slice()) {
        try { fn.apply(null, args); } catch (e) { F.error('mp.events «' + name + '»', e); }
    }
}
mp.events = {
    add, remove,
    call(name, ...args) { dispatch(name, args); },
    callLocal(name, ...args) { dispatch(name, args); },
    callRemote(name, ...args) { F.send(String(name), JSON.stringify(args)); },
    getAllOf(name) { return (listeners.get(name) || []).slice(); },
    reset() { listeners.clear(); },
};

// ---- таймеры ----
let lastTimer = 0;
const timers = new Map();
function timer(fn, ms, repeat, args) {
    if (typeof fn !== 'function') throw new TypeError('setTimeout: нужна функция');
    const every = Math.max(0, Number(ms) || 0);
    const id = ++lastTimer;
    timers.set(id, { fn, at: F.now() + every, every: repeat ? Math.max(1, every) : 0, args });
    return id;
}
globalThis.setTimeout = (fn, ms, ...a) => timer(fn, ms, false, a);
globalThis.setInterval = (fn, ms, ...a) => timer(fn, ms, true, a);
globalThis.setImmediate = (fn, ...a) => timer(fn, 0, false, a);
globalThis.clearTimeout = globalThis.clearInterval = globalThis.clearImmediate = id => { timers.delete(id); };

// ---- консоль ----
function text(v) {
    if (typeof v === 'string') return v;
    if (v instanceof Error) return v.stack || String(v);
    try { const s = JSON.stringify(v); return s === undefined ? String(v) : s; } catch (e) { return String(v); }
}
globalThis.console = {
    log: (...a) => F.log(0, a.map(text).join(' ')),
    info: (...a) => F.log(0, a.map(text).join(' ')),
    debug: (...a) => F.log(0, a.map(text).join(' ')),
    warn: (...a) => F.log(1, a.map(text).join(' ')),
    error: (...a) => F.log(2, a.map(text).join(' ')),
};
mp.console = {
    logInfo: t => F.log(0, text(t)), logWarning: t => F.log(1, text(t)),
    logError: t => F.log(2, text(t)), logFatal: t => F.log(2, text(t)), clear() {},
};

// ---- клавиши: mp.keys как в RAGE:MP ----
const binds = [];
const keyState = new Map();
mp.keys = {
    bind(vk, down, fn) {
        if (typeof fn !== 'function') throw new TypeError('mp.keys.bind: нужна функция');
        binds.push({ vk: vk | 0, down: !!down, fn });
    },
    unbind(vk, down, fn) {
        for (let i = binds.length - 1; i >= 0; i--) {
            const b = binds[i];
            if (b.vk === (vk | 0) && b.down === !!down && (!fn || b.fn === fn)) binds.splice(i, 1);
        }
    },
    isDown: vk => F.keyDown(vk | 0),
    isUp: vk => !F.keyDown(vk | 0),
};

// ---- интерфейс ----
mp.gui = mp.gui || {};
mp.gui.chat = {
    push: t => F.chatPush(String(t)),
    show: v => F.chatShow(!!v),
    activate: v => F.chatShow(!!v),
    colors: true,
    safeMode: false,
};
mp.gui.cursor = {
    show(freeze, show) { F.cursor(!!show, !!freeze); },
    get visible() { return F.cursorVisible(); },
    set visible(v) { F.cursor(!!v, !!v); },
};

// ---- браузеры: mp.browsers как в RAGE:MP ----
// Страница — любой адрес http(s):// или файл пакета: package://ui/index.html
// (папка ui в client_packages). В странице есть window.mp: mp.trigger(имя, …)
// приходит сюда в mp.events, browser.call(имя, …) — в mp.events.add страницы.
const browsers = new Map();
let chatBrowser = null;
class Browser {
    constructor(id, url) { this.id = id; this.remoteId = id; this._url = url; this._active = true; this._orderId = id; this._inputEnabled = true; this._frameRate = 60; this._bounds = null; }
    get type() { return 'browser'; }
    get url() { return this._url; }
    set url(u) { this._url = String(u); F.brUrl(this.id, this._url); }
    get active() { return this._active; }
    set active(v) { this._active = !!v; F.brShow(this.id, this._active); }
    get orderId() { return this._orderId; }
    set orderId(v) { this._orderId = Number.isFinite(Number(v)) ? Math.trunc(Number(v)) : 0; F.brOrder(this.id, this._orderId); }
    get inputEnabled() { return this._inputEnabled; }
    set inputEnabled(v) { this._inputEnabled = !!v; F.brInput(this.id, this._inputEnabled); }
    get frameRate() { return this._frameRate; }
    set frameRate(v) { this._frameRate = Math.max(1, Math.min(60, Math.trunc(Number(v) || 1))); F.brRate(this.id, this._frameRate); }
    get bounds() { return this._bounds ? { ...this._bounds } : null; }
    set bounds(v) {
        if (v == null) { this._bounds = null; F.brBounds(this.id, 0, 0, 0, 0); return; }
        if (typeof v !== 'object') throw new TypeError('browser.bounds: нужен {x,y,width,height} или null');
        const nx = Number(v.x ?? 0), ny = Number(v.y ?? 0), nw = Number(v.width), nh = Number(v.height);
        if (![nx, ny, nw, nh].every(Number.isFinite))
            throw new RangeError('browser.bounds: нужны конечные x/y и width/height больше нуля');
        const maxSide = 7680;
        const tx = Math.trunc(nx), ty = Math.trunc(ny), tw = Math.trunc(nw), th = Math.trunc(nh);
        if (tw < 1 || th < 1)
            throw new RangeError('browser.bounds: width/height после округления должны быть не меньше 1');
        const x = Math.max(-maxSide, Math.min(maxSide, tx));
        const y = Math.max(-maxSide, Math.min(maxSide, ty));
        const width = Math.min(maxSide, tw), height = Math.min(maxSide, th);
        this._bounds = { x, y, width, height };
        F.brBounds(this.id, x, y, width, height);
    }
    setBounds(x, y, width, height) { this.bounds = { x, y, width, height }; return this; }
    resetBounds() { this.bounds = null; return this; }
    focus() { F.brFocus(this.id, true); return this; }
    blur() { F.brFocus(this.id, false); return this; }
    get focused() { return F.brFocused() === this.id; }
    execute(code) { F.brExec(this.id, String(code)); }
    call(name, ...args) { F.brCall(this.id, String(name), JSON.stringify(args)); }
    reload(ignoreCache) { F.brReload(this.id, !!ignoreCache); }
    destroy() {
        if (browsers.get(this.id) !== this) return;
        browsers.delete(this.id);
        if (chatBrowser === this) { chatBrowser = null; F.chatShow(true); }
        F.brDel(this.id);
        dispatch('browserDestroyed', [this]);
    }
    // Страница заменяет встроенный чат: сообщения уходят в chatAPI.push(текст) страницы.
    markAsChat() { chatBrowser = this; F.chatShow(false); }
}
mp.browsers = {
    new(url) {
        const id = F.brNew(String(url));
        if (!id) throw new Error(F.brAvailable() ? 'mp.browsers.new: не удалось создать браузер (проверьте лимит ресурсов)'
                                                  : 'mp.browsers.new: браузеры не установлены у игрока (FloVMP\\cef)');
        const b = new Browser(id, String(url));
        browsers.set(id, b);
        dispatch('browserCreated', [b]);
        return b;
    },
    at(id) { return browsers.get(id) || null; },
    atRemoteId(id) { return browsers.get(id) || null; },
    exists(b) { return !!b && browsers.get(b.id) === b; },
    forEach(fn) { for (const b of [...browsers.values()]) fn(b, b.id); },
    toArray() { return [...browsers.values()]; },
    get length() { return browsers.size; },
    get max() { return F.brMax(); },
    get focused() { return browsers.get(F.brFocused()) || null; },
    get stats() {
        try { return JSON.parse(F.brStats()); }
        catch (e) { return { count: browsers.size, visible: 0, maxBrowsers: F.brMax() }; }
    },
};
globalThis.__flovBrowserHostState = restored => {
    for (const br of [...browsers.values()]) dispatch(restored ? 'browserRestored' : 'browserCrashed', [br]);
};
globalThis.__flovBrowserEvent = (kind, id, a, b) => {
    const br = browsers.get(id);
    if (!br) return;
    if (kind === 'dom') dispatch('browserDomReady', [br]);
    else if (kind === 'fail') dispatch('browserLoadingFailed', [br, Number(a), String(b)]);
    else if (kind === 'trigger') {
        let args = [];
        try { args = JSON.parse(b); } catch (e) {}
        dispatch(a, Array.isArray(args) ? args : [args]);
    }
};
const chatPush = mp.gui.chat.push;
mp.gui.chat.push = t => {
    if (chatBrowser) chatBrowser.execute('window.chatAPI&&chatAPI.push(' + JSON.stringify(String(t)) + ')');
    else chatPush(t);
};
globalThis.__flovChatToBrowser = t => {
    if (!chatBrowser) return false;
    chatBrowser.execute('window.chatAPI&&chatAPI.push(' + JSON.stringify(String(t)) + ')');
    return true;
};

// ---- то, что в RAGE:MP есть поверх нативов ----
// Старый заголовок нативов знает эту функцию как addTextComponentString (тот же
// хэш); скрипты с RAGE:MP зовут её новым именем.
const g = mp.game;
g.ui.addTextComponentSubstringPlayerName = g.ui.addTextComponentString;
// mp.game.graphics.drawText(текст, [x, y], { font, color, scale, outline, centre })
// — одна строка текста на кадр, координаты экрана 0..1.
g.graphics.drawText = function (t, pos, o) {
    o = o || {};
    const c = o.color || [255, 255, 255, 255];
    const s = o.scale || [0.35, 0.35];
    g.ui.setTextFont(o.font | 0);
    g.ui.setTextScale(+s[0], +s[1]);
    g.ui.setTextColour(c[0] | 0, c[1] | 0, c[2] | 0, c[3] === undefined ? 255 : c[3] | 0);
    g.ui.setTextCentre(o.centre !== false);
    if (o.outline) g.ui.setTextOutline();
    g.ui.setTextEntry('STRING');
    g.ui.addTextComponentString(String(t));
    g.ui.drawText(+pos[0], +pos[1]);
};

// ---- игрок ----
mp.players = mp.players || {};
mp.players.local = {
    get handle() { return F.localHandle(); },
    get remoteId() { return F.localId(); },
    get id() { return F.localId(); },
    get name() { return F.localName(); },
    get position() { return F.localPos(); },
    get heading() { return F.localHeading(); },
    get type() { return 'player'; },
};

// ---- require: модули пакета, как в RAGE:MP ----
const modules = new Map();
function normalize(dir, p) {
    if (typeof p !== 'string' || !p) throw new TypeError('require: путь — строка');
    const relative = p.startsWith('./') || p.startsWith('../');
    const parts = (relative && dir ? dir.split('/') : []).concat(p.split('/'));
    const out = [];
    for (const s of parts) {
        if (!s || s === '.') continue;
        if (s === '..') {
            if (!out.length) throw new Error('require: путь выходит за пределы пакета: ' + p);
            out.pop();
        } else out.push(s);
    }
    return out.join('/');
}
function load(path) {
    const candidates = [path, path + '.js', path + '.json', path + '/index.js'];
    for (const c of candidates) {
        const cached = modules.get(c);
        if (cached) return cached.exports;
        const src = F.read(c);
        if (src === null) continue;
        const module = { exports: {}, id: c, filename: c, loaded: false };
        modules.set(c, module);
        if (c.endsWith('.json')) { module.exports = JSON.parse(src); module.loaded = true; return module.exports; }
        const dir = c.includes('/') ? c.slice(0, c.lastIndexOf('/')) : '';
        const fn = F.compile(src, c);
        fn.call(module.exports, module, module.exports, makeRequire(dir), c, dir);
        module.loaded = true;
        return module.exports;
    }
    throw new Error('require: не найден модуль «' + path + '»');
}
function makeRequire(dir) { return p => load(normalize(dir, p)); }
globalThis.require = makeRequire('');

// ---- вход платформы ----
globalThis.__flovMain = () => load('index.js');
globalThis.__flovDispatch = (name, args) => dispatch(name, Array.isArray(args) ? args : []);
globalThis.__flovTick = function (blocked) {
    const now = F.now();
    for (const [id, t] of timers) {
        if (now < t.at) continue;
        if (t.every) t.at = now + t.every; else timers.delete(id);
        try { t.fn.apply(null, t.args); } catch (e) { F.error('таймер', e); }
    }
    const seen = new Set();
    for (const b of binds) {
        if (seen.has(b.vk)) continue;
        seen.add(b.vk);
        const down = F.keyDown(b.vk);
        const was = keyState.get(b.vk) || false;
        keyState.set(b.vk, down);
        if (blocked || down === was) continue;
        for (const x of binds.slice()) {
            if (x.vk !== b.vk || x.down !== down) continue;
            try { x.fn(); } catch (e) { F.error('mp.keys ' + b.vk, e); }
        }
    }
    dispatch('render', []);
};
})();
)JS";

        bool Guarded(JSValue v, const char* where)
        {
            const bool ok = !JS_IsException(v);
            if (!ok) Say(2, std::string(where) + ": " + DescribeException(g_ctx));
            JS_FreeValue(g_ctx, v);
            return ok;
        }

        bool CallGlobal(const char* fn, int argc, JSValue* argv, ULONGLONG budget)
        {
            JSValue global = JS_GetGlobalObject(g_ctx);
            JSValue f = JS_GetPropertyStr(g_ctx, global, fn);
            g_deadline = GetTickCount64() + budget;
            JSValue r = JS_Call(g_ctx, f, global, argc, argv);
            const bool ok = Guarded(r, fn);
            RunJobs();
            g_deadline = 0;
            JS_FreeValue(g_ctx, f);
            JS_FreeValue(g_ctx, global);
            return ok;
        }

        void Stop()
        {
            if (g_ctx) { JS_FreeContext(g_ctx); g_ctx = nullptr; }
            if (g_rt) { JS_FreeRuntime(g_rt); g_rt = nullptr; }
            g_runningDigest.clear();
            g_cursor = false;
            g_cursorFreeze = false;
            browser::DestroyAll();   // страницы сервера живут, пока жив его код
        }

        bool CreateRuntime()
        {
            Stop();
            g_rt = JS_NewRuntime();
            if (!g_rt) return false;
            JS_SetMemoryLimit(g_rt, kMemoryLimit);
            JS_SetMaxStackSize(g_rt, kStackLimit);
            JS_SetInterruptHandler(g_rt, Interrupt, nullptr);
            g_ctx = JS_NewContext(g_rt);
            if (!g_ctx) { Stop(); return false; }

            JSValue global = JS_GetGlobalObject(g_ctx);
            JSValue F = JS_NewObject(g_ctx);
            AddFn(g_ctx, F, "read", F_read, 1);
            AddFn(g_ctx, F, "compile", F_compile, 2);
            AddFn(g_ctx, F, "send", F_send, 2);
            AddFn(g_ctx, F, "log", F_log, 2);
            AddFn(g_ctx, F, "error", F_error, 2);
            AddFn(g_ctx, F, "now", F_now, 0);
            AddFn(g_ctx, F, "keyDown", F_keyDown, 1);
            AddFn(g_ctx, F, "chatPush", F_chatPush, 1);
            AddFn(g_ctx, F, "chatShow", F_chatShow, 1);
            AddFn(g_ctx, F, "cursor", F_cursor, 2);
            AddFn(g_ctx, F, "cursorVisible", F_cursorVisible, 0);
            AddFn(g_ctx, F, "localHandle", F_localHandle, 0);
            AddFn(g_ctx, F, "localPos", F_localPos, 0);
            AddFn(g_ctx, F, "localHeading", F_localHeading, 0);
            AddFn(g_ctx, F, "localId", F_localId, 0);
            AddFn(g_ctx, F, "localName", F_localName, 0);
            AddFn(g_ctx, F, "brNew", F_brNew, 1);
            AddFn(g_ctx, F, "brDel", F_brDel, 1);
            AddFn(g_ctx, F, "brUrl", F_brUrl, 2);
            AddFn(g_ctx, F, "brExec", F_brExec, 2);
            AddFn(g_ctx, F, "brCall", F_brCall, 3);
            AddFn(g_ctx, F, "brShow", F_brShow, 2);
            AddFn(g_ctx, F, "brInput", F_brInput, 2);
            AddFn(g_ctx, F, "brOrder", F_brOrder, 2);
            AddFn(g_ctx, F, "brRate", F_brRate, 2);
            AddFn(g_ctx, F, "brReload", F_brReload, 2);
            AddFn(g_ctx, F, "brBounds", F_brBounds, 5);
            AddFn(g_ctx, F, "brFocus", F_brFocus, 2);
            AddFn(g_ctx, F, "brFocused", F_brFocused, 0);
            AddFn(g_ctx, F, "brAvailable", F_brAvailable, 0);
            AddFn(g_ctx, F, "brMax", F_brMax, 0);
            AddFn(g_ctx, F, "brStats", F_brStats, 0);
            JS_SetPropertyStr(g_ctx, global, "__flov", F);

            // mp.game: все нативы по пространствам + invoke.
            JSValue mp = JS_NewObject(g_ctx);
            JSValue game = JS_NewObject(g_ctx);
            std::map<std::string, JSValue> spaces;
            for (int i = 0; i < (int)(sizeof kNatives / sizeof kNatives[0]); ++i)
            {
                const NativeInfo& n = kNatives[i];
                auto it = spaces.find(n.ns);
                if (it == spaces.end()) it = spaces.emplace(n.ns, JS_NewObject(g_ctx)).first;
                JSValue fn = JS_NewCFunctionData2(g_ctx, CallNative, n.name, (int)strlen(n.args), i, 0, nullptr);
                JS_SetPropertyStr(g_ctx, it->second, n.name, fn);
            }
            for (auto& [name, obj] : spaces) JS_SetPropertyStr(g_ctx, game, name.c_str(), obj);
            const char* kInvoke[] = { "invoke", "invokeFloat", "invokeString", "invokeVector3", "invokeBool" };
            for (int k = 0; k < 5; ++k)
                JS_SetPropertyStr(g_ctx, game, kInvoke[k], JS_NewCFunctionMagic(g_ctx, InvokeHash, kInvoke[k], 1, JS_CFUNC_generic_magic, k));
            JS_SetPropertyStr(g_ctx, mp, "game", game);
            JS_SetPropertyStr(g_ctx, global, "mp", mp);
            JS_FreeValue(g_ctx, global);

            g_deadline = GetTickCount64() + kStartBudgetMs;
            JSValue r = JS_Eval(g_ctx, kPrelude, strlen(kPrelude), "<flovmp>", JS_EVAL_TYPE_GLOBAL);
            g_deadline = 0;
            if (!Guarded(r, "пролог")) { Stop(); return false; }
            return true;
        }

        void FlushInbox()
        {
            while (g_ctx && !g_inbox.empty())
            {
                auto [name, json] = std::move(g_inbox.front());
                g_inbox.pop_front();
                JSValue args = JS_ParseJSON(g_ctx, json.c_str(), json.size(), "<событие>");
                if (JS_IsException(args)) { Say(1, "событие «" + name + "»: аргументы — не JSON: " + DescribeException(g_ctx)); continue; }
                JSValue argv[2] = { JS_NewString(g_ctx, name.c_str()), args };
                CallGlobal("__flovDispatch", 2, argv, kCallBudgetMs);
                JS_FreeValue(g_ctx, argv[0]);
                JS_FreeValue(g_ctx, argv[1]);
            }
        }

        /// События браузеров → mp.events скрипта.
        void FlushBrowserEvents()
        {
            for (auto& e : browser::TakeEvents())
            {
                if (!g_ctx) return;
                using K = browser::Event::Kind;
                if (e.kind == K::Console)
                {
                    const int level = std::clamp(atoi(e.a.c_str()), 0, 2);
                    Say(level, "[страница " + std::to_string(e.id) + "] " + e.b);
                    continue;
                }
                if (e.kind == K::HostRecoveryFailed)
                {
                    Say(2, "браузеры: хост падает слишком часто — страницы сервера отключены до переподключения");
                    JSValue argv[2] = { JS_NewString(g_ctx, "browserHostRecoveryFailed"), JS_NewArray(g_ctx) };
                    CallGlobal("__flovDispatch", 2, argv, kCallBudgetMs);
                    JS_FreeValue(g_ctx, argv[0]);
                    JS_FreeValue(g_ctx, argv[1]);
                    continue;
                }
                if (e.kind == K::HostLost || e.kind == K::HostRestored)
                {
                    const bool restored = e.kind == K::HostRestored;
                    Say(restored ? 0 : 1, restored ? "браузеры: хост восстановлен" : "браузеры: хост закрылся, поднимаю заново");
                    JSValue args = JS_NewArray(g_ctx);
                    JSValue argv[2] = { JS_NewString(g_ctx, restored ? "browserHostRestored" : "browserHostLost"), args };
                    CallGlobal("__flovDispatch", 2, argv, kCallBudgetMs);
                    JSValue state = JS_NewBool(g_ctx, restored);
                    CallGlobal("__flovBrowserHostState", 1, &state, kCallBudgetMs);
                    JS_FreeValue(g_ctx, state);
                    JS_FreeValue(g_ctx, argv[0]);
                    JS_FreeValue(g_ctx, argv[1]);
                    continue;
                }
                const char* kind = e.kind == K::DomReady ? "dom" : e.kind == K::LoadFailed ? "fail" : "trigger";
                JSValue argv[4] = { JS_NewString(g_ctx, kind), JS_NewInt32(g_ctx, e.id),
                                    JS_NewString(g_ctx, e.a.c_str()), JS_NewString(g_ctx, e.b.c_str()) };
                CallGlobal("__flovBrowserEvent", 4, argv, kCallBudgetMs);
                for (auto& v : argv) JS_FreeValue(g_ctx, v);
            }
        }

        bool StartFrom(const std::wstring& dir, const std::string& digest)
        {
            g_packageDir = dir;
            browser::SetPackageRoot(dir);
            if (!CreateRuntime()) { Say(2, "не удалось создать движок JS"); return false; }
            g_runningDigest = digest;
            if (!CallGlobal("__flovMain", 0, nullptr, kStartBudgetMs))
            {
                Say(2, "index.js не запустился — клиентский код сервера отключён до переподключения.");
                Stop();
                return false;
            }
            Say(0, "клиентский код сервера запущен");
            Emit("playerReady");
            FlushInbox();
            return true;
        }

        // --- скачивание пакета ----------------------------------------------------------------
        void Download(int generation, std::string base, std::string digest, std::wstring dir)
        {
            auto fail = [&](const std::string& why)
            {
                Say(2, "клиентские пакеты не скачаны: " + why);
                std::lock_guard lock(g_dl);
                if (generation == g_generation) g_pending = false;
            };
            const std::wstring listPath = dir + L"\\.flovmp-manifest.txt";
            CreateDirs(listPath);
            if (!http::Download(base + "/manifest.txt", listPath)) return fail("список не получен (" + base + ")");
            std::string list;
            if (!ReadFileUtf8(listPath, list)) return fail("список не прочитан");

            struct Entry { std::string path; long long size; std::string sha; };
            std::vector<Entry> files;
            std::string listDigest;
            std::istringstream in(list);
            std::string line;
            long long total = 0;
            while (std::getline(in, line))
            {
                if (!line.empty() && line.back() == '\r') line.pop_back();
                if (line.empty()) continue;
                const size_t t1 = line.find('\t'), t2 = t1 == std::string::npos ? t1 : line.find('\t', t1 + 1);
                if (line.rfind("digest\t", 0) == 0) { listDigest = line.substr(7); continue; }
                if (t2 == std::string::npos) return fail("список повреждён");
                Entry e{ line.substr(0, t1), _atoi64(line.substr(t1 + 1, t2 - t1 - 1).c_str()), line.substr(t2 + 1) };
                if (!ValidPackagePath(e.path) || e.size < 0 || e.sha.size() != 64)
                    return fail("сервер прислал недопустимый файл «" + e.path + "»");
                total += e.size;
                files.push_back(std::move(e));
            }
            if (listDigest != digest) return fail("список не совпал с объявленным сервером");
            {
                std::lock_guard lock(g_dl);
                if (generation != g_generation) return;
                g_dlTotal = total;
                g_dlDone = 0;
            }

            for (const auto& e : files)
            {
                if (generation != g_generation) return;
                const std::wstring local = LocalPath(dir, e.path);
                WIN32_FILE_ATTRIBUTE_DATA info{};
                const bool exists = GetFileAttributesExW(local.c_str(), GetFileExInfoStandard, &info) != 0;
                const long long size = exists ? ((long long)info.nFileSizeHigh << 32 | info.nFileSizeLow) : -1;
                if (!(size == e.size && HexSha256File(local) == e.sha))
                {
                    CreateDirs(local);
                    std::string url = base + "/files/";
                    for (unsigned char c : e.path)
                    {
                        if (isalnum(c) || c == '/' || c == '.' || c == '-' || c == '_') url += (char)c;
                        else { char hex[4]; snprintf(hex, sizeof hex, "%%%02X", c); url += hex; }
                    }
                    if (!http::Download(url, local)) return fail("не скачан " + e.path);
                    if (HexSha256File(local) != e.sha)
                    {
                        DeleteFileW(local.c_str());
                        return fail("файл " + e.path + " скачан с ошибкой (SHA-256 не совпал)");
                    }
                }
                std::lock_guard lock(g_dl);
                g_dlDone += e.size;
            }

            // Файлы прошлой версии, которых в новом списке нет, — убрать: иначе
            // require нашёл бы удалённый владельцем модуль.
            std::map<std::wstring, bool> wanted;
            for (const auto& e : files)
            {
                std::wstring k = LocalPath(dir, e.path);
                std::transform(k.begin(), k.end(), k.begin(), towlower);
                wanted[k] = true;
            }
            std::vector<std::wstring> stack{ dir };
            while (!stack.empty())
            {
                const std::wstring d = stack.back();
                stack.pop_back();
                WIN32_FIND_DATAW fd{};
                HANDLE h = FindFirstFileW((d + L"\\*").c_str(), &fd);
                if (h == INVALID_HANDLE_VALUE) continue;
                do
                {
                    const std::wstring name = fd.cFileName;
                    if (name == L"." || name == L"..") continue;
                    const std::wstring full = d + L"\\" + name;
                    if (fd.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) continue;
                    if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) { stack.push_back(full); continue; }
                    if (name.rfind(L".flovmp-", 0) == 0) continue;
                    std::wstring k = full;
                    std::transform(k.begin(), k.end(), k.begin(), towlower);
                    if (!wanted.count(k)) DeleteFileW(full.c_str());
                } while (FindNextFileW(h, &fd));
                FindClose(h);
            }

            std::lock_guard lock(g_dl);
            if (generation != g_generation) return;
            g_pending = false;
            g_readyToStart = true;
            g_readyDir = dir;
            g_readyDigest = digest;
        }
    }

    void SetNativeBackend(const NativeBackend& backend) { g_backend = backend; }

    void SetLocalPlayer(int remoteId, const std::string& name)
    {
        g_localId = remoteId;
        g_localName = name;
    }

    void OnPackagesAnnounced(const std::string& host, const std::string& source, const std::string& digest,
                             int count, long long totalBytes)
    {
        if (digest.size() != 64 || count <= 0) return;
        if (digest == g_runningDigest) return;   // тот же набор уже работает
        std::string base;
        if (!source.empty() && source[0] == ':') base = "http://" + host + source + "/client";
        else return;   // адрес раздачи только свой: чужой CDN для кода не принимаем
        const std::wstring dir = DataDir() + L"\\packages\\" + FromUtf8(http::CacheName(host + source).substr(0, 16));
        int generation;
        {
            std::lock_guard lock(g_dl);
            generation = ++g_generation;
            g_pending = true;
            g_readyToStart = false;
            g_dlDone = 0;
            g_dlTotal = totalBytes;
        }
        Say(0, "клиентские пакеты сервера: " + std::to_string(count) + " файлов, " +
               std::to_string(totalBytes / 1024) + " КБ — проверяю и докачиваю");
        std::thread(Download, generation, base, digest, dir).detach();
    }

    Progress DownloadProgress()
    {
        std::lock_guard lock(g_dl);
        return { g_pending, g_dlDone, g_dlTotal };
    }

    bool Pending()
    {
        std::lock_guard lock(g_dl);
        return g_pending || g_readyToStart;
    }

    void Tick(bool inputBlocked)
    {
        std::wstring startDir;
        std::string startDigest;
        {
            std::lock_guard lock(g_dl);
            if (g_readyToStart)
            {
                g_readyToStart = false;
                startDir = g_readyDir;
                startDigest = g_readyDigest;
            }
        }
        if (!startDir.empty()) StartFrom(startDir, startDigest);
        if (!g_ctx) return;
        FlushInbox();
        FlushBrowserEvents();
        JSValue blocked = JS_NewBool(g_ctx, inputBlocked);
        CallGlobal("__flovTick", 1, &blocked, kCallBudgetMs);
    }

    void OnServerEvent(const std::string& name, const std::string& argsJson)
    {
        if (g_inbox.size() >= kMaxQueuedEvents) g_inbox.pop_front();
        g_inbox.emplace_back(name, argsJson.empty() ? "[]" : argsJson);
    }

    void Emit(const std::string& name, const std::string& argsJson) { OnServerEvent(name, argsJson); }

    std::vector<std::pair<std::string, std::string>> TakeOutgoing()
    {
        std::vector<std::pair<std::string, std::string>> out;
        out.swap(g_outbox);
        return out;
    }

    void Reset()
    {
        {
            std::lock_guard lock(g_dl);
            ++g_generation;
            g_pending = false;
            g_readyToStart = false;
        }
        Stop();
        browser::NewSession();
        g_inbox.clear();
        g_outbox.clear();
        g_packageDir.clear();
        // Новая сессия не наследует лимит событий прошлой.
        g_outCount = 0;
        g_outWindow = 0;
    }

    bool ChatToBrowser(const std::string& text)
    {
        if (!g_ctx) return false;
        JSValue global = JS_GetGlobalObject(g_ctx);
        JSValue f = JS_GetPropertyStr(g_ctx, global, "__flovChatToBrowser");
        JSValue arg = JS_NewString(g_ctx, text.c_str());
        g_deadline = GetTickCount64() + kCallBudgetMs;
        JSValue r = JS_Call(g_ctx, f, global, 1, &arg);
        g_deadline = 0;
        const bool taken = !JS_IsException(r) && JS_ToBool(g_ctx, r) == 1;
        if (JS_IsException(r)) Say(2, "чат в страницу: " + DescribeException(g_ctx));
        JS_FreeValue(g_ctx, r);
        JS_FreeValue(g_ctx, arg);
        JS_FreeValue(g_ctx, f);
        JS_FreeValue(g_ctx, global);
        return taken;
    }

    bool CursorWanted() { return g_ctx && g_cursor; }
    bool CursorFreeze() { return g_ctx && g_cursor && g_cursorFreeze; }

    bool RunSource(const std::string& fileName, const std::string& source)
    {
        if (!CreateRuntime()) return false;
        g_deadline = GetTickCount64() + kStartBudgetMs;
        JSValue r = JS_Eval(g_ctx, source.c_str(), source.size(), fileName.c_str(), JS_EVAL_TYPE_GLOBAL);
        g_deadline = 0;
        const bool ok = Guarded(r, fileName.c_str());
        RunJobs();
        return ok;
    }

    bool RunFolder(const std::wstring& folder) { return StartFrom(folder, "local"); }

    bool Running() { return g_ctx != nullptr; }
}
