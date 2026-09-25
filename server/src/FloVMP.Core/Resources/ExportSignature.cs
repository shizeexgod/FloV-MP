namespace FloVMP.Core.Resources;

/// <summary>
/// Подпись экспортируемой функции ресурса (пункт 24 roadmap): типы
/// аргументов и результата строкой, например
/// <c>System.Int32,System.String-&gt;System.Threading.Tasks.Task`1[System.Int32]</c>.
///
/// Зачем: штатный Alt.Import проверяет только «это функция», а значения
/// приводит молча — живой прогон показал, что строковый экспорт,
/// импортированный как функция с числом, проходит Import и падает уже при
/// вызове случайным FormatException. Экспортёр публикует подпись рядом с
/// функцией, вызывающий сверяет её до первого вызова.
/// </summary>
public static class ExportSignature
{
    /// <summary>Какие типы проходят между ресурсами без потерь.</summary>
    public static bool Supported(Type t)
    {
        if (t.IsArray) return t.GetArrayRank() == 1 && Supported(t.GetElementType()!);
        if (t == typeof(Task)) return true;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>)) return Supported(t.GetGenericArguments()[0]);
        return t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(uint) || t == typeof(ulong) ||
               t == typeof(double) || t == typeof(float) || t == typeof(string) || t == typeof(byte) || t == typeof(short);
    }

    /// <summary>Подпись для делегата Func/Action.</summary>
    public static string Of(Type delegateType)
    {
        var invoke = delegateType.GetMethod("Invoke") ?? throw new ArgumentException("не делегат: " + delegateType);
        foreach (var p in invoke.GetParameters())
            if (!Supported(p.ParameterType))
                throw new NotSupportedException($"тип аргумента {p.ParameterType.Name} не проходит между ресурсами: " +
                                                "только числа, bool, string, их массивы и Task<…>");
        if (invoke.ReturnType != typeof(void) && !Supported(invoke.ReturnType))
            throw new NotSupportedException($"тип результата {invoke.ReturnType.Name} не проходит между ресурсами");
        return string.Join(",", invoke.GetParameters().Select(p => p.ParameterType.FullName)) + "->" +
               (invoke.ReturnType == typeof(void) ? "void" : invoke.ReturnType.FullName);
    }

    /// <summary>Имя, под которым рядом с функцией лежит её подпись.</summary>
    public static string KeyFor(string name) => name + "#flovmp-sig";

    /// <summary>Имя экспорта: латиница, цифры, «_», «.», «-», не длиннее 64.</summary>
    public static bool ValidName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 64 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');

    /// <summary>Понятное описание расхождения для исключения.</summary>
    public static string Describe(string resource, string name, string expected, string? actual) =>
        actual is null
            ? $"ресурс {resource} не экспортирует {name} через FloVExports (или не запущен)"
            : $"{resource}.{name}: у экспорта подпись ({actual}), а вызывается как ({expected})";
}
