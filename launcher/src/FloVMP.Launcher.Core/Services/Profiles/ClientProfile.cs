using System.Text.Json.Serialization;
using FloVMP.Launcher.Services.Compat;

namespace FloVMP.Launcher.Services.Profiles;

public sealed class ClientProfile
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("edition")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GtaEdition Edition { get; set; } = GtaEdition.Legacy;

    [JsonPropertyName("gameExecutable")]
    public string GameExecutable { get; set; } = "GTA5.exe";

    [JsonPropertyName("gameFileVersion")]
    public string GameFileVersion { get; set; } = "";

    [JsonPropertyName("gameSha256")]
    public string? GameSha256 { get; set; }

    [JsonPropertyName("updateRpfSha256")]
    public string? UpdateRpfSha256 { get; set; }

    [JsonPropertyName("update2RpfSha256")]
    public string? Update2RpfSha256 { get; set; }

    [JsonPropertyName("runtimeClientVersion")]
    public string RuntimeClientVersion { get; set; } = "";

    [JsonPropertyName("runtimeLauncherVersion")]
    public string RuntimeLauncherVersion { get; set; } = "";

    [JsonPropertyName("supportStatus")]
    public string SupportStatus { get; set; } = "unknown";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("offsets")]
    public Dictionary<string, string> Offsets { get; set; } = new();
}

public sealed record ProfileValidationResult(
    bool IsMatch,
    bool AllHashesValid,
    IReadOnlyList<string> Issues,
    ClientProfile? MatchedProfile = null);
