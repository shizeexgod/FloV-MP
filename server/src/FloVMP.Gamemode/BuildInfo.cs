using System.Reflection;

namespace FloVMP.Gamemode;

/// <summary>
/// Метаданные сборки геймода. Версия берётся из файла VERSION в корне
/// репозитория (server/Directory.Build.props).
/// </summary>
public static class BuildInfo
{
    /// <summary>Версия FloV:MP (не путать с версией движка alt:V).</summary>
    public static string Version { get; } =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? typeof(BuildInfo).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
