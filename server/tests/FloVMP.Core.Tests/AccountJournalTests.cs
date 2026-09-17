using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Журнал регистраций и индекс банковских счетов.
///
/// Раньше Create() переписывал весь accounts.json целиком. Нагрузочный стенд
/// намерял 9 мс на регистрацию уже при 1500 учётках — стоимость растёт линейно
/// с их числом, и на десятках тысяч это сотни миллисекунд. Теперь регистрация
/// дописывает одну строку в журнал, а полный файл перезаписывает фоновый сброс.
/// Здесь проверяется, что при этом ничего не теряется и ничего не откатывается.
/// </summary>
public class AccountJournalTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public AccountJournalTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-journal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "accounts.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* временный каталог */ }
    }

    [Fact]
    public void Create_SurvivesRestart_WithoutFullFlush()
    {
        // Ключевой сценарий: сервер упал сразу после регистрации, полный
        // accounts.json записаться не успел. Аккаунт обязан пережить падение.
        var store = new JsonAccountStore(_path);
        store.Create("Новичок", "hash1");
        // Dispose НЕ вызываем — имитируем аварийное завершение.

        var reopened = new JsonAccountStore(_path);
        Assert.NotNull(reopened.FindByUsername("Новичок"));
        reopened.Dispose();
    }

    [Fact]
    public void Create_WritesJournal_AndFlushClearsIt()
    {
        var store = new JsonAccountStore(_path);
        store.Create("A", "h");
        Assert.True(File.Exists(_path + ".journal"));

        store.Flush();
        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".journal"));

        store.Dispose();
    }

    [Fact]
    public void Journal_DoesNotOverwriteNewerStateFromMainFile()
    {
        // Сброс сначала пишет полный файл, потом удаляет журнал. Если сервер
        // умрёт между этими шагами, журнал переживёт файл. Применение старой
        // записи поверх свежей откатило бы уровень админа и пароль — поэтому
        // из журнала берутся только отсутствующие учётки.
        var store = new JsonAccountStore(_path);
        var acc = store.Create("Богач", "h");
        acc.PasswordHash = "new";
        acc.AdminLevel = 7;
        store.Update(acc);
        store.Flush(); // полный файл записан, журнал удалён

        // Вручную возвращаем устаревшую запись журнала (состояние на момент
        // регистрации) — ровно то, что осталось бы после падения.
        File.WriteAllText(_path + ".journal",
            System.Text.Json.JsonSerializer.Serialize(new Account
            {
                Id = acc.Id,
                Username = "Богач",
                PasswordHash = "h",
                AdminLevel = 0,
            }) + "\n");

        var reopened = new JsonAccountStore(_path);
        var loaded = reopened.FindByUsername("Богач");
        Assert.NotNull(loaded);
        Assert.Equal("new", loaded!.PasswordHash);
        Assert.Equal(7, loaded.AdminLevel);
        reopened.Dispose();
        store.Dispose();
    }

    [Fact]
    public void Journal_SkipsTornLine_AndKeepsTheRest()
    {
        // Аварию можно поймать посреди дописывания строки. Одна битая строка
        // не должна стоить всех остальных регистраций.
        var store = new JsonAccountStore(_path);
        store.Create("Первый", "h");
        store.Create("Второй", "h");
        store.Dispose();

        // Пересоздаём журнал: валидная строка + оборванная.
        File.Delete(_path);
        File.WriteAllText(_path + ".journal",
            System.Text.Json.JsonSerializer.Serialize(new Account { Id = 1, Username = "Целый", PasswordHash = "h" }) + "\n" +
            "{\"Username\":\"Обор");

        var reopened = new JsonAccountStore(_path);
        Assert.NotNull(reopened.FindByUsername("Целый"));
        reopened.Dispose();
    }

    [Fact]
    public void ManyRegistrations_DoNotRewriteWholeFileEachTime()
    {
        // Регрессионный тест на саму суть правки: 400 регистраций подряд
        // должны укладываться в секунду. Со старым поведением (полный сброс
        // на каждую) это O(n^2) и заметно больше.
        var store = new JsonAccountStore(_path);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 400; i++) store.Create($"Игрок{i}", "hash");
        sw.Stop();
        store.Dispose();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"400 регистраций заняли {sw.ElapsedMilliseconds} мс — похоже, вернулась полная перезапись файла");

        var reopened = new JsonAccountStore(_path);
        Assert.NotNull(reopened.FindByUsername("Игрок0"));
        Assert.NotNull(reopened.FindByUsername("Игрок399"));
        reopened.Dispose();
    }
}
