using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FloVMP.Core.Characters;

/// <summary>
/// Универсальный сервис выдачи служебной формы фракций.
/// Полностью отвязан от конкретной карты или RP-сеттинга (чистый .NET 8).
/// Серверные гейммоды регистрируют свои пресеты и логику выдачи формы через этот реестр.
/// </summary>
public sealed class FactionUniformService
{
    private readonly ConcurrentDictionary<int, Action<CharacterAppearance, int>> _uniformAppliers = new();

    /// <summary>
    /// Глобальный экземпляр по умолчанию.
    /// </summary>
    public static FactionUniformService Default { get; } = new();

    /// <summary>
    /// Зарегистрировать логику униформы для конкретной фракции.
    /// </summary>
    /// <param name="factionId">Идентификатор фракции.</param>
    /// <param name="applier">Делегат применения формы: (appearance, rankLevel) -> void.</param>
    public void RegisterUniform(int factionId, Action<CharacterAppearance, int> applier)
    {
        _uniformAppliers[factionId] = applier;
    }

    /// <summary>
    /// Удалить регистрацию формы для фракции.
    /// </summary>
    public bool UnregisterUniform(int factionId)
    {
        return _uniformAppliers.TryRemove(factionId, out _);
    }

    /// <summary>
    /// Проверить, зарегистрирована ли форма для фракции.
    /// </summary>
    public bool HasUniform(int factionId)
    {
        return _uniformAppliers.ContainsKey(factionId);
    }

    /// <summary>
    /// Применить служебную форму фракции к персонажу.
    /// </summary>
    /// <returns>True, если для фракции настроена форма и она была применена; иначе false.</returns>
    public bool ApplyUniform(CharacterAppearance appearance, int factionId, int rankLevel)
    {
        if (_uniformAppliers.TryGetValue(factionId, out var applier))
        {
            applier(appearance, rankLevel);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Очистить все зарегистрированные формы.
    /// </summary>
    public void Clear()
    {
        _uniformAppliers.Clear();
    }

    /// <summary>
    /// Статический фасад для быстрого применения через экземпляр по умолчанию.
    /// </summary>
    public static bool Apply(CharacterAppearance appearance, int factionId, int rankLevel)
    {
        return Default.ApplyUniform(appearance, factionId, rankLevel);
    }
}
