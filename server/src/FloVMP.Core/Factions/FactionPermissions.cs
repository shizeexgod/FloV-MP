using System;

namespace FloVMP.Core.Factions;

/// <summary>
/// Битовые флаги разрешений и должностных полномочий внутри фракции.
/// </summary>
[Flags]
public enum FactionPermissions : uint
{
    None = 0,

    /// <summary>Приглашение новых членов во фракцию</summary>
    Invite = 1 << 0,

    /// <summary>Увольнение сотрудников из фракции</summary>
    Kick = 1 << 1,

    /// <summary>Повышение ранга сотрудников</summary>
    Promote = 1 << 2,

    /// <summary>Понижение ранга сотрудников</summary>
    Demote = 1 << 3,

    /// <summary>Доступ к оружейному складу / выдаче табельного оружия</summary>
    Armory = 1 << 4,

    /// <summary>Доступ к материальному складу / хранилищу предметов</summary>
    Storage = 1 << 5,

    /// <summary>Снятие денежных средств из казны фракции</summary>
    TreasuryWithdraw = 1 << 6,

    /// <summary>Пополнение казны фракции</summary>
    TreasuryDeposit = 1 << 7,

    /// <summary>Рация фракции (/f, /r)</summary>
    RadioFaction = 1 << 8,

    /// <summary>Департаментская государственная рация (/d)</summary>
    RadioDepartment = 1 << 9,

    /// <summary>Применение наручников / стяжек (/cuff, /uncuff)</summary>
    Cuffs = 1 << 10,

    /// <summary>Обыск карманов и инвентаря граждан (/search)</summary>
    SearchInventory = 1 << 11,

    /// <summary>Выписка административных и судебных штрафов (/ticket)</summary>
    IssueFine = 1 << 12,

    /// <summary>Арест и водворение в ИВС / КПЗ (/arrest)</summary>
    Arrest = 1 << 13,

    /// <summary>Оказание первой помощи / реанимация и лечение (/heal)</summary>
    Heal = 1 << 14,

    /// <summary>Прямой эфир и вещание новостей (/news)</summary>
    NewsBroadcast = 1 << 15,

    /// <summary>Полные полномочия лидера организации</summary>
    All = uint.MaxValue
}
