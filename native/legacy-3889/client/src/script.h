#pragma once
// Клиентский код сервера (пункт 26 roadmap): server/client_packages у игрока.
//
// Как в RAGE:MP: владелец сервера пишет index.js и сколько угодно модулей,
// интерфейсы, картинки. Клиент скачивает их во время загрузки, сверяет
// каждый файл по SHA-256 и исполняет index.js в движке quickjs.
//
// Изоляция. У скрипта нет доступа к файлам, процессам и сети игрока: в движке
// нет модулей std/os, require читает только файлы скачанного пакета. Нативы
// GTA доступны все — ровно как в RAGE:MP. Зависший скрипт прерывается по
// времени, а не вешает игру.
//
// Потоки. Скачивание — в своём потоке. Всё, что касается JS, — только в
// игровом потоке (из него же вызываются нативы).

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

namespace flov::script
{
    /// Как вызывать нативы. В игре — ScriptHookV, в тестах — подмена.
    struct NativeBackend
    {
        void (*begin)(uint64_t hash) = nullptr;
        void (*push)(uint64_t value) = nullptr;
        uint64_t* (*call)() = nullptr;
    };
    void SetNativeBackend(const NativeBackend& backend);

    /// Сведения об игроке для mp.players.local (игровой поток сообщает сам).
    void SetLocalPlayer(int remoteId, const std::string& name);

    /// Сервер сообщил о пакетах: CPKG. host — адрес, к которому подключились.
    void OnPackagesAnnounced(const std::string& host, const std::string& source, const std::string& digest,
                             int count, long long totalBytes);

    /// Прогресс скачивания для экрана загрузки.
    struct Progress { bool active = false; long long done = 0, total = 0; };
    Progress DownloadProgress();

    /// Пакеты ещё не готовы (качаются) — экрану загрузки стоит подождать.
    bool Pending();

    /// Каждый кадр из игрового потока: запуск скачанного, таймеры, клавиши,
    /// событие render.
    void Tick(bool inputBlocked);

    /// Событие с сервера: CEV имя [аргументы JSON].
    void OnServerEvent(const std::string& name, const std::string& argsJson);

    /// Встроенные события платформы для скриптов: playerSpawn, playerDeath…
    void Emit(const std::string& name, const std::string& argsJson = "[]");

    /// События, которые скрипт отправил на сервер (mp.events.callRemote).
    std::vector<std::pair<std::string, std::string>> TakeOutgoing();

    /// Отключились от сервера — остановить скрипт и забыть его состояние.
    void Reset();

    /// Запустить код из строки — для тестов и средства предпросмотра.
    bool RunSource(const std::string& fileName, const std::string& source);

    /// Запустить пакет из папки на диске — для тестов и отладки.
    bool RunFolder(const std::wstring& folder);

    /// Скрипт сейчас работает.
    bool Running();

    /// Мир для клиентского кода: mp.players, mp.vehicles — как в RAGE:MP.
    /// Заполняет игровой поток (game.cpp), читает JS в том же потоке.
    struct PlayerView
    {
        bool ok = false;
        std::string name;
        int handle = 0;          // 0 — не в зоне видимости
        float x = 0, y = 0, z = 0, heading = 0;
        bool hasPosition = false;
        int health = 0, armor = 0;   // 0..100, как player.health в RAGE:MP
        int vehicle = 0;         // ID машины реестра, 0 — пешком
        int seat = -2;           // -1 — водитель
    };
    struct VehicleView
    {
        bool ok = false;
        int handle = 0;
        uint32_t model = 0;
        std::string plate;
        float x = 0, y = 0, z = 0, heading = 0;
        bool engine = false, locked = false;
        int driver = 0;
    };
    struct WorldBackend
    {
        std::vector<int> (*players)() = nullptr;           // все игроки сервера, включая себя
        PlayerView (*player)(int id) = nullptr;
        const std::string* (*variable)(int id, const std::string& key) = nullptr;   // JSON или nullptr
        std::vector<std::string> (*variableKeys)(int id) = nullptr;
        std::vector<int> (*vehicles)() = nullptr;
        VehicleView (*vehicle)(int id) = nullptr;
    };
    void SetWorldBackend(const WorldBackend& backend);

    /// playerJoin / playerQuit / entityStreamIn / entityStreamOut. name — для
    /// playerQuit: игрока уже нет в списке, а обработчику нужно его имя.
    void EntityEvent(const std::string& event, int id, const std::string& name = "");
    /// Переменная игрока изменилась (SVAR): mp.events.addDataHandler.
    void DataChange(int id, const std::string& key, const std::string& json, const std::string& oldJson);

    /// mp.nametags.enabled = false — встроенные ники платформы выключены
    /// скриптом (владелец рисует свои).
    bool NametagsDisabledByScript();

    /// Скрипт показал курсор (mp.gui.cursor.show) и хочет ли заморозить персонажа.
    bool CursorWanted();
    bool CursorFreeze();

    /// Строка чата — в страницу, которую скрипт сделал чатом (markAsChat).
    /// false — такой страницы нет, строка идёт во встроенный чат.
    bool ChatToBrowser(const std::string& text);
}
