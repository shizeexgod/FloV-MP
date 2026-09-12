using System;

namespace FloVMP.Core.AntiCheat;

/// <summary>
/// Структурированное событие детекта античита.
/// Позволяет RP-проектам перехватывать любые подозрительные действия,
/// отправлять алерты в Discord, логировать в БД или применять кастомные наказания.
/// </summary>
public record AntiCheatDetectionEvent
{
    public int AccountId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DetectionType { get; init; } = string.Empty; // "Teleport", "SpeedHack", "NoClip", "BlacklistedWeapon", "GodMode", "EventSpam"
    public AntiCheatSeverity Severity { get; init; } = AntiCheatSeverity.Medium;
    public string Details { get; init; } = string.Empty;
    public Vector3D Location { get; init; } = Vector3D.Zero;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public AntiCheatAction SuggestedAction { get; init; } = AntiCheatAction.Warning;
}
