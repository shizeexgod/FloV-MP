namespace FloVMP.Core.Licensing;

/// <summary>
/// Что владельцу сервера разрешено менять в оформлении платформы.
///
/// Обычная лицензия — это FloV:MP: заголовок окна игры, название на
/// загрузочном экране и в консоли остаются «FloV:MP». Свой бренд — у Source
/// Kit: либо тариф в подписанной лицензии (source, source-kit), либо сервер,
/// собранный из выданных исходников (<see cref="SourceKitBuild"/> выставляет
/// scripts/export_source.py).
/// </summary>
public static class Edition
{
    /// <summary>true в сборке из комплекта исходников (Source Kit).</summary>
    public const bool SourceKitBuild = false;

    private static string? _version;

    /// <summary>
    /// Версия установленной платформы: читается из файла VERSION в корне
    /// установки. Нужна, чтобы сервер видел, у кого из игроков клиент старее,
    /// и мог попросить обновиться.
    /// </summary>
    public static string Version => _version ??= ReadVersion();

    private static string ReadVersion()
    {
        var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts)
        {
            var dir = new DirectoryInfo(start);
            for (var i = 0; dir is not null && i < 5; i++, dir = dir.Parent)
            {
                var file = Path.Combine(dir.FullName, "VERSION");
                try
                {
                    if (File.Exists(file))
                    {
                        var text = File.ReadAllText(file).Trim();
                        if (text.Length is > 0 and <= 40) return text;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return "неизвестна";
    }

    private static readonly string[] BrandingPlans = { "source", "source-kit", "sourcekit" };

    public static bool BrandingAllowed(LicenseInfo? license) =>
        SourceKitBuild || (license is not null &&
            BrandingPlans.Contains(license.Plan.Trim(), StringComparer.OrdinalIgnoreCase));
}
