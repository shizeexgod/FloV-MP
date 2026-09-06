namespace FloVMP.Core.Documents;

/// <summary>
/// Тип документа, удостоверяющего личность или предоставляющего специальное право.
/// </summary>
public enum DocumentType
{
    None = 0,

    /// <summary>Паспорт гражданина Российской Федерации</summary>
    Passport = 1,

    /// <summary>Водительское удостоверение (категории A, B, C, D, Водный, Авиа)</summary>
    DriverLicense = 2,

    /// <summary>Лицензия на ношение и хранение оружия (Росгвардия / ОЛРР)</summary>
    WeaponLicense = 3,

    /// <summary>Медицинская книжка / справка о здоровье (психиатр, нарколог)</summary>
    MedicalCard = 4,

    /// <summary>Трудовая книжка (записи о трудовом стаже и службе)</summary>
    WorkRecord = 5
}
