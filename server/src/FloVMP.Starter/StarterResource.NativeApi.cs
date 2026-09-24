using System.Globalization;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;

namespace FloVMP.Starter;

/// <summary>
/// Публичный API платформы для игроков GTA V Legacy 1.0.3889.0.
///
/// У такого игрока нет сущности движка alt:V, поэтому чужой ресурс не может
/// получить его через Alt.GetPlayerById и управлять им напрямую — это
/// закрывало разработчику сервера всё, кроме чата и меню. Здесь те же
/// действия, что платформа делает своими командами, вынесены событиями:
/// ресурс шлёт событие с ID игрока, платформа выполняет.
///
/// Все события принимают ID игрока первым аргументом. Если игрока нет, в
/// журнал уходит предупреждение, а сервер продолжает работать.
/// </summary>
public partial class StarterResource
{
    private void RegisterNativeApi()
    {
        // --- управление игроком -------------------------------------------------------
        Alt.OnServer<int, string>("flovmp:native:kick", (id, reason) =>
            WithNative(id, "kick", p => p.Kick(Clean(reason, 200))));

        Alt.OnServer<int, float, float, float>("flovmp:native:teleport", (id, x, y, z) =>
            WithNative(id, "teleport", p =>
            {
                if (!Finite(x) || !Finite(y) || !Finite(z)) { Alt.LogWarning("[FloV:MP] flovmp:native:teleport: недопустимые координаты"); return; }
                p.Position = new Position(x, y, z);
                NotifyTeleport(p);
            }));

        Alt.OnServer<int, float, float, float, float>("flovmp:native:spawn", (id, x, y, z, heading) =>
            WithNative(id, "spawn", p =>
            {
                if (!Finite(x) || !Finite(y) || !Finite(z)) { Alt.LogWarning("[FloV:MP] flovmp:native:spawn: недопустимые координаты"); return; }
                p.Spawn(new Position(x, y, z), 0);
                if (Finite(heading)) p.Rotation = new Rotation(0, 0, heading * MathF.PI / 180f);
                NotifyTeleport(p);
            }));

        Alt.OnServer<int, int>("flovmp:native:health", (id, value) =>
            WithNative(id, "health", p => p.Health = (ushort)Math.Clamp(value, 0, 200)));

        Alt.OnServer<int, int>("flovmp:native:armor", (id, value) =>
            WithNative(id, "armor", p => p.Armor = (ushort)Math.Clamp(value, 0, 100)));

        Alt.OnServer<int, string>("flovmp:native:model", (id, model) =>
            WithNative(id, "model", p =>
            {
                var name = (model ?? "").Trim();
                if (name.Length == 0) return;
                var hash = uint.TryParse(name, out var raw) ? raw : Alt.Hash(name.ToLowerInvariant());
                SetModelWithName(p, hash, name);
            }));

        Alt.OnServer<int, string, int>("flovmp:native:weapon", (id, weapon, ammo) =>
            WithNative(id, "weapon", p =>
            {
                var name = (weapon ?? "").Trim();
                if (name.Length == 0) return;
                var hash = uint.TryParse(name, out var raw) ? raw : Alt.Hash(name.ToLowerInvariant());
                GiveWeaponWithName(p, hash, name, Math.Clamp(ammo, 0, 9999));
            }));

        Alt.OnServer<int>("flovmp:native:disarm", (id) =>
            WithNative(id, "disarm", p => p.RemoveAllWeapons(true)));

        Alt.OnServer<int, int>("flovmp:native:dimension", (id, dimension) =>
            WithNative(id, "dimension", p => p.Dimension = dimension));

        // --- чтение состояния ---------------------------------------------------------
        // Ответ приходит событием flovmp:native:state. Так ресурс узнаёт
        // позицию, здоровье, броню, оружие и транспорт игрока 3889.
        Alt.OnServer<int>("flovmp:native:query", id => EmitNativeState(id));

        // --- бой ------------------------------------------------------------------------
        // Ответ на событие flovmp:damage. Звать надо прямо внутри обработчика
        // того события: позже попадание уже засчитано, и ответ просто
        // отбрасывается — чужому выстрелу он не достанется.
        Alt.OnServer<int, bool, int>("flovmp:damage:set", (request, allow, damage) =>
        {
            if (!_damage.Answer(request, allow, damage))
                Alt.LogWarning($"[FloV:MP] flovmp:damage:set: ответ на чужой или закрытый вопрос {request}");
        });

        Alt.OnServer("flovmp:native:queryAll", () =>
        {
            foreach (var id in _nativePlayers.Keys.ToList()) EmitNativeState((int)id);
        });
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && MathF.Abs(value) < 100000f;

    /// <summary>Игрок 3889 по ID: действие выполняется, только если он на сервере.</summary>
    private void WithNative(int id, string what, Action<IPlayer> action)
    {
        var player = NativeById(id);
        if (player is null || !player.Exists)
        {
            Alt.LogWarning($"[FloV:MP] flovmp:native:{what}: игрока 3889 с ID {id} нет на сервере");
            return;
        }
        try { action(player); }
        catch (Exception ex) { Alt.LogError($"[FloV:MP] flovmp:native:{what} для ID {id}: {ex.Message}"); }
    }

    /// <summary>
    /// Состояние игрока 3889 ресурсу: ID, ник, позиция, курс, здоровье, броня,
    /// оружие, измерение, в транспорте ли он и модель транспорта.
    /// </summary>
    private void EmitNativeState(int id)
    {
        var player = NativeById(id);
        if (player is null || !player.Exists) return;
        var np = (NativePlayerProxy)(object)player;
        var s = np.State;
        var pos = player.Position;
        // Клиент 1.0.6+: машина и место — из реестра, поля STATE у него не значат ничего.
        var (vehicleModel, seat) = (s.VehicleModel, s.Seat);
        if (UsesRegistry(np.Session.Id))
        {
            var rv = _vehicles?.Registry.VehicleOf(np.Session.Id);
            (vehicleModel, seat) = rv is null ? (0u, -1) : (rv.Model, _vehicles!.Registry.SeatOf(np.Session.Id).Seat);
        }
        Alt.Emit("flovmp:native:state", id, player.Name,
            pos.X, pos.Y, pos.Z, s.Heading,
            (int)player.Health, (int)player.Armor,
            s.Weapon.ToString(CultureInfo.InvariantCulture),
            np.DimensionValue,
            s.InVehicle,
            vehicleModel.ToString(CultureInfo.InvariantCulture),
            seat);
    }
}
