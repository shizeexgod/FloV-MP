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

    private static readonly string[] BrandingPlans = { "source", "source-kit", "sourcekit" };

    public static bool BrandingAllowed(LicenseInfo? license) =>
        SourceKitBuild || (license is not null &&
            BrandingPlans.Contains(license.Plan.Trim(), StringComparer.OrdinalIgnoreCase));
}
