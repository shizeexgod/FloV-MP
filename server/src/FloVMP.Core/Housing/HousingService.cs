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

    public HousingService()
    {
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

}
