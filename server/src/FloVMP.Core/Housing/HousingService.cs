using System;
using System.Collections.Generic;
using System.Linq;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Auth;

namespace FloVMP.Core.Housing;

/// <summary>
/// Сервис управления рынком недвижимости, заселением, сейфами и замками квартир/домов.
/// Чистый .NET 8, потокобезопасен.
/// </summary>
public sealed class HousingService
{
    private readonly object _lock = new();
    private readonly Dictionary<int, Property> _properties = new();

    public const int MaxPropertiesPerPlayer = 3;
    public const double SellRefundPercent = 0.75;

    public HousingService(bool loadDefaultPresets = true)
    {
        if (loadDefaultPresets)
        {
            LoadDefaultPresets();
        }
    }

    public void RegisterProperty(Property property)
    {
        lock (_lock)
        {
            _properties[property.Id] = property;
        }
    }

    public Property? GetProperty(int id)
    {
        lock (_lock)
        {
            return _properties.TryGetValue(id, out var p) ? p : null;
        }
    }

    public IReadOnlyList<Property> GetAllProperties()
    {
        lock (_lock)
        {
            return _properties.Values.ToList();
        }
    }

    public IReadOnlyList<Property> GetPropertiesByOwner(int ownerAccountId)
    {
        lock (_lock)
        {
            return _properties.Values.Where(p => p.OwnerAccountId == ownerAccountId).ToList();
        }
    }

    public Property? GetNearbyProperty(Vector3D pos, float radius = 3.0f)
    {
        lock (_lock)
        {
            return _properties.Values.FirstOrDefault(p =>
                p.EntrancePosition.DistanceTo(pos) <= radius || p.InteriorPosition.DistanceTo(pos) <= radius);
        }
    }

    public bool TryBuy(Account account, int propertyId, out string error)
    {
        lock (_lock)
        {
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (prop.HasOwner)
            {
                error = "У этого объекта уже есть владелец";
                return false;
            }

            var currentOwned = _properties.Values.Count(p => p.OwnerAccountId == account.Id);
            if (currentOwned >= MaxPropertiesPerPlayer)
            {
                error = $"Превышен лимит недвижимости (максимум {MaxPropertiesPerPlayer} объекта на гражданина)";
                return false;
            }

            if (account.Bank < prop.Price)
            {
                error = $"Недостаточно средств на банковском счёте. Требуется: {prop.Price:N0} руб., у вас: {account.Bank:N0} руб.";
                return false;
            }

            account.Bank -= prop.Price;
            prop.OwnerAccountId = account.Id;
            prop.IsLocked = false;
            prop.Roommates.Clear();
            error = string.Empty;
            return true;
        }
    }

    public bool TrySell(Account account, int propertyId, out long refundAmount, out string error)
    {
        lock (_lock)
        {
            refundAmount = 0;
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (prop.OwnerAccountId != account.Id)
            {
                error = "Вы не являетесь владельцем этого объекта";
                return false;
            }

            refundAmount = (long)(prop.Price * SellRefundPercent);
            var safeRefund = prop.SafeCash;

            account.Bank += refundAmount + safeRefund;

            prop.OwnerAccountId = null;
            prop.IsLocked = true;
            prop.SafeCash = 0;
            prop.Roommates.Clear();
            error = string.Empty;
            return true;
        }
    }

    public bool TryToggleLock(int accountId, int propertyId, out bool isLocked, out string error)
    {
        lock (_lock)
        {
            isLocked = false;
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (!prop.HasAccess(accountId))
            {
                error = "У вас нет ключей от этого объекта";
                return false;
            }

            prop.IsLocked = !prop.IsLocked;
            isLocked = prop.IsLocked;
            error = string.Empty;
            return true;
        }
    }

    public bool TryDepositSafe(Account account, int propertyId, long amount, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма пополнения должна быть больше нуля";
            return false;
        }

        lock (_lock)
        {
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (prop.OwnerAccountId != account.Id)
            {
                error = "Доступ к сейфу имеет только владелец объекта";
                return false;
            }

            if (account.Cash < amount)
            {
                error = "У вас недостаточно наличных средств";
                return false;
            }

            account.Cash -= amount;
            prop.SafeCash += amount;
            error = string.Empty;
            return true;
        }
    }

    public bool TryWithdrawSafe(Account account, int propertyId, long amount, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма снятия должна быть больше нуля";
            return false;
        }

        lock (_lock)
        {
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (prop.OwnerAccountId != account.Id)
            {
                error = "Доступ к сейфу имеет только владелец объекта";
                return false;
            }

            if (prop.SafeCash < amount)
            {
                error = $"В сейфе недостаточно средств (доступно: {prop.SafeCash:N0} руб.)";
                return false;
            }

            prop.SafeCash -= amount;
            account.Cash += amount;
            error = string.Empty;
            return true;
        }
    }

    public bool TryAddRoommate(int ownerAccountId, int propertyId, int roommateAccountId, out string error)
    {
        lock (_lock)
        {
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (prop.OwnerAccountId != ownerAccountId)
            {
                error = "Вы не являетесь владельцем этого объекта";
                return false;
            }

            if (ownerAccountId == roommateAccountId)
            {
                error = "Вы уже являетесь владельцем";
                return false;
            }

            if (!prop.Roommates.Add(roommateAccountId))
            {
                error = "Игрок уже заселён в этот объект";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public bool TryRemoveRoommate(int ownerAccountId, int propertyId, int roommateAccountId, out string error)
    {
        lock (_lock)
        {
            if (!_properties.TryGetValue(propertyId, out var prop))
            {
                error = "Объект недвижимости не найден";
                return false;
            }

            if (prop.OwnerAccountId != ownerAccountId)
            {
                error = "Вы не являетесь владельцем этого объекта";
                return false;
            }

            if (!prop.Roommates.Remove(roommateAccountId))
            {
                error = "Игрок не был заселён в этот объект";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    private void LoadDefaultPresets()
    {
        // 1. ЖК «Москва-Сити» (Апартаменты Башня Федерация)
        RegisterProperty(new Property(
            1,
            "Пресненская наб., 12, Башня Федерация, Ап. 42",
            PropertyType.Apartment,
            price: 25_000_000,
            entrance: new Vector3D(-268.3f, -956.8f, 31.2f),
            interior: new Vector3D(-786.8f, 315.7f, 217.6f),
            dimension: 1001));

        // 2. ЖК «Тверской Бульвар» (Квартира в центре)
        RegisterProperty(new Property(
            2,
            "Тверской бульвар, д. 15, кв. 8",
            PropertyType.Apartment,
            price: 8_500_000,
            entrance: new Vector3D(112.5f, -820.4f, 31.1f),
            interior: new Vector3D(340.9f, 437.1f, 149.3f),
            dimension: 1002));

        // 3. Посёлок «Барвиха Luxury» (Коттедж на Рублёвке)
        RegisterProperty(new Property(
            3,
            "Рублёво-Успенское ш., пос. Барвиха, Владение 7",
            PropertyType.Mansion,
            price: 85_000_000,
            entrance: new Vector3D(-1288.6f, 440.3f, 97.5f),
            interior: new Vector3D(1397.3f, 1141.2f, 114.3f),
            dimension: 1003));

        // 4. Гаражный кооператив «Москвич» (Гараж №24)
        RegisterProperty(new Property(
            4,
            "Южнопортовая ул., ГСК Москвич, Бокс 24",
            PropertyType.Garage,
            price: 1_200_000,
            entrance: new Vector3D(740.1f, -1002.5f, 22.8f),
            interior: new Vector3D(178.6f, -1005.8f, -98.9f),
            dimension: 1004));
    }
}
