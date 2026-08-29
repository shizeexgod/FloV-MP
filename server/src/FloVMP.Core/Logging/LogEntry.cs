namespace FloVMP.Core.Logging;

/// <summary>Категории логов (см. docs/admin/admin-and-logging.md, раздел 1).</summary>
public static class LogCategory
{
    public const string Admin = "admin";          // действия администрации
    public const string Account = "account";      // регистрация/вход/смена данных
    public const string Character = "character";  // создание/удаление/переименование
    public const string Punishment = "punishment";// баны/муты/арест/штрафы
    public const string Money = "money";          // все дельты денег с источником
    public const string Item = "item";            // предметы/склад
    public const string Vehicle = "vehicle";      // транспорт
    public const string Org = "org";              // фракции/бизнесы/семьи/дома
    public const string Identity = "identity";    // смена связок ID/имён
    public const string Event = "event";          // мероприятия
    public const string Kill = "kill";            // убийства
    public const string Qa = "qa";                // вопросы игроков
    public const string Promo = "promo";          // промо/бонус-коды
    public const string System = "system";        // служебное (старт/стоп и т.п.)
}

public enum ActorKind { System, Player, Admin }

public sealed record LogActor(ActorKind Kind, int AccountId = 0, string Name = "")
{
    public static readonly LogActor SystemActor = new(ActorKind.System, 0, "system");
    public static LogActor Player(int accountId, string name) => new(ActorKind.Player, accountId, name);
    public static LogActor Admin(int accountId, string name) => new(ActorKind.Admin, accountId, name);
}

/// <summary>
/// Одна запись лога. Иммутабельна, только добавляется. Сериализуется в
/// JSON Lines файловым синком и (позже) в БД.
/// </summary>
public sealed record LogEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string TsUtc { get; init; } = DateTime.UtcNow.ToString("O");
    public string Category { get; init; } = LogCategory.System;
    public string Action { get; init; } = "";
    public LogActor Actor { get; init; } = LogActor.SystemActor;

    /// <summary>Кого/что затронуло (accountId игрока, id объекта и т.п.). Может быть пусто.</summary>
    public string Target { get; init; } = "";

    /// <summary>Произвольные детали (что выдали, координаты, старое/новое значение…).</summary>
    public IReadOnlyDictionary<string, object?> Details { get; init; }
        = new Dictionary<string, object?>();

    public string Ip { get; init; } = "";
}
