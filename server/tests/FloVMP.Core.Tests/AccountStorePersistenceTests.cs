using System;
using System.IO;
using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Запись аккаунтов идёт в фоне с дебаунсом (чтобы не блокировать игровой тик).
/// Эти тесты гарантируют, что данные всё равно доходят до диска.
/// </summary>
public class AccountStorePersistenceTests
{
    [Fact]
    public void Flush_PersistsChanges_Immediately()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_acc_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonAccountStore(tempFile);
            var acc = store.Create("TestPlayer", "pbkdf2$sha256$1$c2FsdA==$aGFzaA==");
            acc.AdminLevel = 5;
            store.Update(acc);

            store.Flush();

            var reloaded = new JsonAccountStore(tempFile);
            Assert.Equal(5, reloaded.FindByUsername("TestPlayer")!.AdminLevel);
            reloaded.Dispose();
            store.Dispose();
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact]
    public void Dispose_FlushesPendingChanges_NoDataLoss()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_acc_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonAccountStore(tempFile);
            var acc = store.Create("ShutdownPlayer", "pbkdf2$sha256$1$c2FsdA==$aGFzaA==");
            acc.Email = "shutdown@example.com";
            store.Update(acc);
            store.Dispose(); // остановка сервера обязана дописать данные

            var reloaded = new JsonAccountStore(tempFile);
            Assert.Equal("shutdown@example.com", reloaded.FindByUsername("ShutdownPlayer")!.Email);
            reloaded.Dispose();
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact]
    public void Update_DoesNotBlockOnDiskWrite()
    {
        // Ключевое свойство: Update() не должен синхронно писать файл.
        // Тысяча изменений должна отрабатывать мгновенно (раньше это была
        // тысяча полных перезаписей файла на игровом потоке).
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_acc_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonAccountStore(tempFile);
            var acc = store.Create("HotPath", "pbkdf2$sha256$1$c2FsdA==$aGFzaA==");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++) { acc.LastLoginUtc = i.ToString(); store.Update(acc); }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500,
                $"1000 обновлений заняли {sw.ElapsedMilliseconds}мс — похоже, запись всё ещё синхронная");

            store.Dispose();
            var reloaded = new JsonAccountStore(tempFile);
            Assert.Equal("999", reloaded.FindByUsername("HotPath")!.LastLoginUtc);
            reloaded.Dispose();
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }
}
