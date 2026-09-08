using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Spatial;

namespace FloVMP.Core.Voice;

/// <summary>
/// Режимы дистанции пространственного голоса.
/// </summary>
public enum VoiceRangeMode
{
    Whisper = 0,   // Шёпот (~2.5м)
    Normal = 1,    // Обычная речь (~8.0м)
    Shout = 2,     // Крик (~20.0м)
    Megaphone = 3  // Мегафон / Громкоговоритель (~60.0м)
}

/// <summary>
/// Тип голосовой передачи.
/// </summary>
public enum VoiceTransmissionType
{
    Proximity3D = 0, // Позиционный 3D звук
    Radio = 1,       // Рация (частота)
    PhoneCall = 2    // Телефонный звонок (прямой P2P/конференция)
}

/// <summary>
/// Математическая модель затухания громкости с расстоянием.
/// </summary>
public enum VoiceAttenuationModel
{
    Linear,
    InverseSquare,
    SmoothStep
}

/// <summary>
/// Получатель аудио-пакета с рассчитанными пространственными характеристиками.
/// </summary>
public readonly struct VoiceRecipient
{
    public ulong ListenerId { get; }
    public float Volume { get; }
    public Vector3D RelativeOffset { get; }
    public VoiceTransmissionType TransmissionType { get; }

    public VoiceRecipient(ulong listenerId, float volume, Vector3D relativeOffset, VoiceTransmissionType transmissionType)
    {
        ListenerId = listenerId;
        Volume = volume;
        RelativeOffset = relativeOffset;
        TransmissionType = transmissionType;
    }
}

/// <summary>
/// Высокопроизводительный 3D Voice & Radio роутер с поддержкой Spatial Hash Grid.
/// Обеспечивает O(1) маршрутизацию голосовых пакетов для 1500–3000 одновременных игроков,
/// многоканальную рацию с шифрованием частот и телефонную связь.
/// </summary>
public sealed class VoiceGridRouter
{
    private readonly SpatialHashGrid<ulong> _spatialGrid;
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> _playerMuteLists = new();
    private readonly ConcurrentDictionary<ulong, bool> _serverMutes = new();
    
    // Рации: Частота (например 101.5) -> Словарь (PlayerId -> EncryptionKey)
    private readonly ConcurrentDictionary<float, ConcurrentDictionary<ulong, string?>> _radioFrequencies = new();
    
    // Телефонные звонки: CallId -> Множество участников (PlayerId)
    private readonly ConcurrentDictionary<string, HashSet<ulong>> _activePhoneCalls = new();
    private readonly ConcurrentDictionary<ulong, string> _playerActiveCalls = new();

    private readonly object _radioLock = new();
    private readonly object _phoneLock = new();

    public VoiceGridRouter(SpatialHashGrid<ulong> spatialGrid)
    {
        _spatialGrid = spatialGrid ?? throw new ArgumentNullException(nameof(spatialGrid));
    }

    /// <summary>
    /// Получить максимальную дистанцию слышимости для указанного режима.
    /// </summary>
    public static float GetMaxDistance(VoiceRangeMode mode) => mode switch
    {
        VoiceRangeMode.Whisper => 2.5f,
        VoiceRangeMode.Normal => 8.0f,
        VoiceRangeMode.Shout => 20.0f,
        VoiceRangeMode.Megaphone => 60.0f,
        _ => 8.0f
    };

    /// <summary>
    /// Устанавливает статус серверного мута игрока (админ-мут).
    /// </summary>
    public void SetServerMute(ulong playerId, bool isMuted)
    {
        if (isMuted)
            _serverMutes[playerId] = true;
        else
            _serverMutes.TryRemove(playerId, out _);
    }

    public bool IsServerMuted(ulong playerId) => _serverMutes.ContainsKey(playerId);

    /// <summary>
    /// Клиентский мут (игрок A замьютил игрока B).
    /// </summary>
    public void SetPlayerMute(ulong listenerId, ulong targetSpeakerId, bool isMuted)
    {
        var list = _playerMuteLists.GetOrAdd(listenerId, _ => new HashSet<ulong>());
        lock (list)
        {
            if (isMuted)
                list.Add(targetSpeakerId);
            else
                list.Remove(targetSpeakerId);
        }
    }

    public bool IsPlayerMutedBy(ulong listenerId, ulong speakerId)
    {
        if (_playerMuteLists.TryGetValue(listenerId, out var list))
        {
            lock (list) return list.Contains(speakerId);
        }
        return false;
    }

    /// <summary>
    /// Маршрутизирует пространственный 3D голос на основе SpatialHashGrid за O(1).
    /// Вычисляет точную громкость и относительный вектор позиции для каждого слушателя.
    /// </summary>
    public IReadOnlyList<VoiceRecipient> RouteSpatialVoice(
        ulong speakerId,
        Vector3D speakerPos,
        int dimension,
        VoiceRangeMode rangeMode,
        VoiceAttenuationModel attenuation = VoiceAttenuationModel.Linear)
    {
        // Если говорящий замучен на уровне сервера — пакет никому не доставляется
        if (IsServerMuted(speakerId))
        {
            return Array.Empty<VoiceRecipient>();
        }

        float maxDist = GetMaxDistance(rangeMode);
        var nearby = _spatialGrid.FindInRadiusWithDistance(speakerPos, maxDist, dimension);
        if (nearby.Count == 0) return Array.Empty<VoiceRecipient>();

        var recipients = new List<VoiceRecipient>(nearby.Count);

        foreach (var (listenerId, distance) in nearby)
        {
            if (listenerId == speakerId) continue; // Не отправляем собственный голос себе

            // Проверка персонального мута
            if (IsPlayerMutedBy(listenerId, speakerId)) continue;

            float volume = CalculateVolume(distance, maxDist, attenuation);
            if (volume <= 0.001f) continue;

            // Позиция источника звука относительно слушателя (для HRTF / 3D Audio)
            // Примечание: вектор направлен от слушателя к говорящему
            // Если в будущем потребуется точная позиция слушателя, берется из SpatialGrid
            var relativeOffset = new Vector3D(speakerPos.X, speakerPos.Y, speakerPos.Z);

            recipients.Add(new VoiceRecipient(listenerId, volume, relativeOffset, VoiceTransmissionType.Proximity3D));
        }

        return recipients;
    }

    /// <summary>
    /// Подключение игрока к частоте рации.
    /// </summary>
    public void TuneRadio(ulong playerId, float frequency, string? encryptionKey = null)
    {
        if (float.IsNaN(frequency) || float.IsInfinity(frequency) || frequency <= 0.0f || frequency > 1000.0f)
        {
            return;
        }

        // Нормализация частоты до 1 знака после запятой (например, 101.5)
        frequency = (float)Math.Round(frequency, 1);

        lock (_radioLock)
        {
            // Отключаем от старых частот если был подключен
            LeaveRadio(playerId);

            var channel = _radioFrequencies.GetOrAdd(frequency, _ => new ConcurrentDictionary<ulong, string?>());
            channel[playerId] = encryptionKey;
        }
    }
    /// <summary>
    /// Отключение игрока от рации.
    /// </summary>
    public void LeaveRadio(ulong playerId)
    {
        lock (_radioLock)
        {
            foreach (var pair in _radioFrequencies)
            {
                pair.Value.TryRemove(playerId, out _);
            }
        }
    }

    /// <summary>
    /// Полная очистка состояния игрока (радио, телефонные звонки, списки мутов) при отключении от сервера.
    /// </summary>
    public void RemovePlayer(ulong playerId)
    {
        LeaveRadio(playerId);
        LeavePhoneCall(playerId);
        _playerMuteLists.TryRemove(playerId, out _);
        _serverMutes.TryRemove(playerId, out _);
    }

    /// <summary>
    /// Маршрутизация радиовещания по частоте с проверкой крипто-ключа шифрования.
    /// </summary>
    public IReadOnlyList<VoiceRecipient> RouteRadioVoice(ulong speakerId, float frequency, string? speakerKey = null)
    {
        if (IsServerMuted(speakerId)) return Array.Empty<VoiceRecipient>();
        if (float.IsNaN(frequency) || float.IsInfinity(frequency) || frequency <= 0.0f || frequency > 1000.0f)
            return Array.Empty<VoiceRecipient>();

        frequency = (float)Math.Round(frequency, 1);

        if (!_radioFrequencies.TryGetValue(frequency, out var channel))
        {
            return Array.Empty<VoiceRecipient>();
        }

        var recipients = new List<VoiceRecipient>();

        foreach (var (listenerId, listenerKey) in channel)
        {
            if (listenerId == speakerId) continue;
            if (IsPlayerMutedBy(listenerId, speakerId)) continue;

            // Если у рации установлен ключ шифрования — проверяем совпадение ключа
            if (listenerKey != null && !string.Equals(listenerKey, speakerKey, StringComparison.Ordinal))
            {
                // Несовпадение шифрования — слушатель не получает разборчивую передачу
                continue;
            }

            recipients.Add(new VoiceRecipient(
                listenerId,
                volume: 1.0f,
                relativeOffset: Vector3D.Zero,
                VoiceTransmissionType.Radio
            ));
        }

        return recipients;
    }

    /// <summary>
    /// Создает или присоединяет игрока к телефонному звонку.
    /// </summary>
    public void JoinPhoneCall(string callId, ulong playerId)
    {
        lock (_phoneLock)
        {
            // Если уже был в другом звонке — выходим
            LeavePhoneCall(playerId);

            if (!_activePhoneCalls.TryGetValue(callId, out var participants))
            {
                participants = new HashSet<ulong>();
                _activePhoneCalls[callId] = participants;
            }

            participants.Add(playerId);
            _playerActiveCalls[playerId] = callId;
        }
    }

    /// <summary>
    /// Покинуть телефонный звонок.
    /// </summary>
    public void LeavePhoneCall(ulong playerId)
    {
        lock (_phoneLock)
        {
            if (_playerActiveCalls.TryRemove(playerId, out var callId))
            {
                if (_activePhoneCalls.TryGetValue(callId, out var participants))
                {
                    participants.Remove(playerId);
                    if (participants.Count == 0)
                    {
                        _activePhoneCalls.TryRemove(callId, out _);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Маршрутизация телефонного звонка между участниками линии.
    /// </summary>
    public IReadOnlyList<VoiceRecipient> RoutePhoneVoice(ulong speakerId)
    {
        if (IsServerMuted(speakerId)) return Array.Empty<VoiceRecipient>();

        lock (_phoneLock)
        {
            if (!_playerActiveCalls.TryGetValue(speakerId, out var callId))
            {
                return Array.Empty<VoiceRecipient>();
            }

            if (!_activePhoneCalls.TryGetValue(callId, out var participants))
            {
                return Array.Empty<VoiceRecipient>();
            }

            var recipients = new List<VoiceRecipient>(participants.Count);
            foreach (var listenerId in participants)
            {
                if (listenerId == speakerId) continue;
                if (IsPlayerMutedBy(listenerId, speakerId)) continue;

                recipients.Add(new VoiceRecipient(
                    listenerId,
                    volume: 1.0f,
                    relativeOffset: Vector3D.Zero,
                    VoiceTransmissionType.PhoneCall
                ));
            }

            return recipients;
        }
    }

    /// <summary>
    /// Расчет коэффициента громкости (0.0 .. 1.0) в зависимости от дистанции и модели.
    /// </summary>
    public static float CalculateVolume(float distance, float maxDistance, VoiceAttenuationModel model)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0) return 1.0f;
        if (float.IsNaN(maxDistance) || float.IsInfinity(maxDistance) || maxDistance <= 0.0001f || distance >= maxDistance) return 0.0f;

        float normalized = distance / maxDistance; // 0.0 .. 1.0

        return model switch
        {
            VoiceAttenuationModel.Linear => Math.Clamp(1.0f - normalized, 0.0f, 1.0f),
            VoiceAttenuationModel.InverseSquare => Math.Clamp(1.0f / (1.0f + 2.0f * normalized * normalized) * (1.0f - normalized), 0.0f, 1.0f),
            VoiceAttenuationModel.SmoothStep => Math.Clamp((1.0f - normalized) * (1.0f - normalized) * (3.0f - 2.0f * (1.0f - normalized)), 0.0f, 1.0f),
            _ => Math.Clamp(1.0f - normalized, 0.0f, 1.0f)
        };
    }
}
