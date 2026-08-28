using AltV.Net;

namespace FloVMP.Gamemode;

/// <summary>
/// Изоляция исключений на границе с alt:V. Любой наш обработчик события
/// движка оборачиваем: брошенное исключение логируем и глотаем, чтобы
/// один кривой пакет от клиента или баг в одной системе не ронял диспетчер
/// событий и весь сервер.
/// </summary>
public static class Safe
{
    public static void Run(string where, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Alt.LogError($"[FloV:MP] исключение в {where}: {ex}");
        }
    }
}
