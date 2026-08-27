using FloVMP.Launcher.Services.Compat;
using Xunit;

namespace FloVMP.Launcher.Tests;

/// <summary>
/// Тесты подмены/восстановления GTA5.exe. Симулируем краш = маркер остался
/// на диске, новый экземпляр менеджера чинит при «старте».
/// </summary>
public sealed class GameExeManagerTests : IDisposable
{
    private readonly string _root;
    private readonly string _gta;
    private readonly string _cache;
    private readonly string _marker;

    public GameExeManagerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "flovmp-swap-tests", Guid.NewGuid().ToString("N"));
        _gta = Path.Combine(_root, "gta");
        _cache = Path.Combine(_root, "cache");
        Directory.CreateDirectory(_gta);
        Directory.CreateDirectory(_cache);
        _marker = Path.Combine(_root, "pending-restore.json");

        File.WriteAllText(Path.Combine(_gta, "GTA5.exe"), "ORIGINAL EXE BYTES v.real");
        File.WriteAllText(Path.Combine(_cache, "GTA5.stable.exe"), "CACHED STABLE EXE BYTES");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private string Target => Path.Combine(_gta, "GTA5.exe");
    private string Backup => Target + ".flovmp-orig";
    private string Cached => Path.Combine(_cache, "GTA5.stable.exe");
    private const long Now = 1_700_000_000_000;

    [Fact]
    public async Task SwapIn_then_Restore_roundtrips()
    {
        var mgr = new GameExeManager(_marker);

        var swap = await mgr.SwapInAsync(_gta, Cached, Now, gamePid: 4321);
        Assert.True(swap.Ok, swap.Message);
        Assert.True(File.Exists(_marker));
        Assert.True(File.Exists(Backup));
        Assert.Equal("CACHED STABLE EXE BYTES", File.ReadAllText(Target));

        var marker = PendingRestore.Load(_marker)!;
        Assert.Equal(4321, marker.GamePid);
        Assert.Equal(Now, marker.CreatedUnixMs);

        var restore = await mgr.RestoreAsync();
        Assert.True(restore.Ok, restore.Message);
        Assert.False(File.Exists(_marker));
        Assert.False(File.Exists(Backup));
        Assert.Equal("ORIGINAL EXE BYTES v.real", File.ReadAllText(Target));
    }

    [Fact]
    public async Task Crash_after_swap_is_repaired_on_next_start()
    {
        // подмена состоялась
        var crashed = new GameExeManager(_marker);
        Assert.True((await crashed.SwapInAsync(_gta, Cached, Now)).Ok);
        Assert.Equal("CACHED STABLE EXE BYTES", File.ReadAllText(Target));

        // ... UI лаунчера умер, отката не было. Новый старт:
        var fresh = new GameExeManager(_marker);
        Assert.True(fresh.HasPendingRestore);

        var fix = await fresh.RestoreAsync();
        Assert.True(fix.Ok, fix.Message);
        Assert.Equal("ORIGINAL EXE BYTES v.real", File.ReadAllText(Target));
        Assert.False(File.Exists(_marker));
    }

    [Fact]
    public async Task SwapIn_refuses_when_marker_already_present()
    {
        var mgr = new GameExeManager(_marker);
        Assert.True((await mgr.SwapInAsync(_gta, Cached, Now)).Ok);

        var second = await mgr.SwapInAsync(_gta, Cached, Now);
        Assert.False(second.Ok);
        Assert.Contains("маркер", second.Message);
    }

    [Fact]
    public async Task SwapIn_is_noop_when_build_identical()
    {
        File.WriteAllText(Cached, File.ReadAllText(Target)); // одинаковое содержимое
        var mgr = new GameExeManager(_marker);

        var res = await mgr.SwapInAsync(_gta, Cached, Now);
        Assert.True(res.Ok);
        Assert.False(File.Exists(_marker));
        Assert.False(File.Exists(Backup));
    }

    [Fact]
    public async Task Restore_is_noop_without_marker()
    {
        var res = await new GameExeManager(_marker).RestoreAsync();
        Assert.True(res.Ok);
    }

    [Fact]
    public async Task Restore_clears_marker_when_original_already_in_place()
    {
        var mgr = new GameExeManager(_marker);
        Assert.True((await mgr.SwapInAsync(_gta, Cached, Now)).Ok);

        // сымитируем: кто-то вернул оригинал руками, бэкап удалил, маркер забыл
        File.Delete(Target);
        File.Move(Backup, Target);

        var res = await mgr.RestoreAsync();
        Assert.True(res.Ok, res.Message);
        Assert.False(File.Exists(_marker));
        Assert.Equal("ORIGINAL EXE BYTES v.real", File.ReadAllText(Target));
    }
}
