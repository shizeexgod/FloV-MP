// FloV:MP — клиент для GTA V Legacy 1.0.3889.0.
//
// ASI-плагин: загружается ASI-загрузчиком (dinput8.dll из комплекта
// ScriptHookV) и работает через ScriptHookV. Без подключения к серверу ничего
// в игре не меняет: одиночная игра остаётся одиночной, пока игрок не зашёл на
// сервер (F9 или запуск через коннектор FloV:MP).

#include <windows.h>
#include "common.h"
#include "crash.h"
#include "game.h"
#include "invoke.h"
#include "ui.h"

namespace
{
    bool g_bound = false;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        flov::Log(std::string("FloV:MP client ") + flov::kClientVersion + " загружен в GTA5.exe " + flov::GameVersion());
        flov::crash::Install();

        // ScriptHookV.dll не импортируется статически — берём уже загруженный
        // или грузим из папки игры. Так ASI не роняет игру, если ScriptHookV нет.
        HMODULE hook = GetModuleHandleW(L"ScriptHookV.dll");
        if (!hook) hook = LoadLibraryW(L"ScriptHookV.dll");
        if (!shv::Bind(hook))
        {
            flov::Log("ScriptHookV.dll не найден или несовместим — установите ScriptHookV для 1.0.3889.0 "
                      "(http://www.dev-c.com/gtav/scripthookv/). Клиент неактивен.");
            return TRUE;
        }
        g_bound = true;
        shv::scriptRegister(module, flov::game::ScriptMain);
        flov::ui::Init();
    }
    else if (reason == DLL_PROCESS_DETACH && g_bound)
    {
        flov::ui::Shutdown();
        if (shv::scriptUnregister) shv::scriptUnregister(module);
        flov::game::Shutdown();
    }
    return TRUE;
}
