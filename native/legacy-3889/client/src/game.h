#pragma once

namespace flov::game
{
    /// Главный цикл скрипта (поток скриптов игры через ScriptHookV).
    void ScriptMain();
    /// Выгрузка: удалить созданные сущности, закрыть соединение.
    void Shutdown();
}
