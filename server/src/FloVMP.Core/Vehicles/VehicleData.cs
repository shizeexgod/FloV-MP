namespace FloVMP.Core.Vehicles;

/// <summary>
/// Данные транспортного средства в мире «Держава Онлайн».
/// Сохраняются в БД (таблица `vehicles`).
/// </summary>
public sealed class VehicleData
{
    public int Id { get; set; }
    public int OwnerAccountId { get; set; }
    public string Model { get; set; } = "sultan";
    public string Plate { get; set; } = "FLOV001";
    public int ColorPrimary { get; set; } = 0;
    public int ColorSecondary { get; set; } = 0;
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }
    public int Dimension { get; set; }
    public float Fuel { get; set; } = 100.0f;
    public float EngineHealth { get; set; } = 1000.0f;
    public float BodyHealth { get; set; } = 1000.0f;
    public bool IsLocked { get; set; } = true;
    public bool EngineOn { get; set; } = false;
    public bool IsImpounded { get; set; } = false;
    public string TrunkJson { get; set; } = "[]";
    public string GloveboxJson { get; set; } = "[]";

    public const float MaxFuel = 100.0f;
    public const float FuelConsumptionPerKm = 0.08f;

    /// <summary>
    /// Проверка, имеет ли игрок доступ к ключам/управлению автомобилем.
    /// </summary>
    public bool CanAccess(int accountId, int adminLevel)
    {
        if (adminLevel >= 4) return true; // Администраторы 4+ уровня имеют мастер-ключ
        return OwnerAccountId == accountId;
    }

    /// <summary>
    /// Переключение состояния замка дверей.
    /// </summary>
    public bool ToggleLock(int accountId, int adminLevel, out string message)
    {
        if (!CanAccess(accountId, adminLevel))
        {
            message = "У вас нет ключей от этого транспортного средства";
            return false;
        }

        IsLocked = !IsLocked;
        message = IsLocked ? "Двери заблокированы" : "Двери разблокированы";
        return true;
    }

    /// <summary>
    /// Переключение двигателя.
    /// </summary>
    public bool ToggleEngine(int accountId, int adminLevel, out string message)
    {
        if (!CanAccess(accountId, adminLevel))
        {
            message = "У вас нет ключей от замка зажигания";
            return false;
        }

        if (Fuel <= 0.1f)
        {
            EngineOn = false;
            message = "Бак пуст! Двигатель не заводится";
            return false;
        }

        if (EngineHealth <= 100.0f)
        {
            EngineOn = false;
            message = "Двигатель повреждён и не может быть запущен";
            return false;
        }

        EngineOn = !EngineOn;
        message = EngineOn ? "Двигатель заведён" : "Двигатель заглушен";
        return true;
    }

    /// <summary>
    /// Расход топлива в зависимости от пройденного расстояния или времени работы на холостом ходу.
    /// </summary>
    public void ConsumeFuel(float amount)
    {
        if (!EngineOn) return;
        Fuel = Math.Clamp(Fuel - amount, 0.0f, MaxFuel);
        if (Fuel <= 0.05f)
        {
            EngineOn = false;
        }
    }
}