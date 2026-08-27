using System.IO;
using System.Windows.Media;
using FloVMP.Launcher.Models;
using FloVMP.Launcher.Mvvm;
using FloVMP.Launcher.Services;
using FloVMP.Launcher.Services.Cdn;
using FloVMP.Launcher.Services.Compat;
using Microsoft.Win32;

namespace FloVMP.Launcher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly LauncherSettings _s;
    private readonly SyncService _sync = new();
    private CancellationTokenSource? _syncCts;

    public MainViewModel()
    {
        _s = SettingsStore.Load();

        CheckServerCommand = new RelayCommand(async () => await CheckServerAsync());
        DetectGtaCommand = new RelayCommand(DetectGta);
        BrowseGtaCommand = new RelayCommand(BrowseGta);
        BrowseCoreCommand = new RelayCommand(BrowseCore);
        SyncCoreCommand = new RelayCommand(async () => await SyncCoreAsync(), () => !IsSyncing && !string.IsNullOrWhiteSpace(_s.CoreManifestUrl));
        PlayCommand = new RelayCommand(async () => await PlayAsync(), () => CanPlay);
        SaveCommand = new RelayCommand(Save);

        if (string.IsNullOrWhiteSpace(_s.AltvCoreDir))
        {
            _s.AltvCoreDir = Path.Combine(SettingsStore.Dir, "client");
        }

        // Автозаполнение при первом старте.
        if (string.IsNullOrWhiteSpace(_s.GtaPath))
        {
            var first = GtaLocator.Detect().FirstOrDefault();
            if (first is not null) _s.GtaPath = first.Path;
        }

        if (string.IsNullOrWhiteSpace(_s.Nickname))
        {
            _s.Nickname = AltvClientCore.ReadAltvTomlValue("name") ?? "";
        }

        RefreshDerived();
        Log("Лаунчер запущен. Настройки: " + SettingsStore.FilePath);
        if (!string.IsNullOrWhiteSpace(_s.GtaPath))
            Log("GTA V: " + _s.GtaPath);
        if (!string.IsNullOrWhiteSpace(App.StartupRestoreNote))
            Log(App.StartupRestoreNote!);
    }

    // --- прокси-свойства к настройкам --------------------------------

    public string ServerHost
    {
        get => _s.ServerHost;
        set { _s.ServerHost = value; OnPropertyChanged(); RefreshDerived(); }
    }

    public string ServerPortText
    {
        get => _s.ServerPort.ToString();
        set
        {
            if (int.TryParse(value, out var p) && p is > 0 and < 65536)
            {
                _s.ServerPort = p;
                OnPropertyChanged();
                RefreshDerived();
            }
        }
    }

    public string Nickname
    {
        get => _s.Nickname;
        set { _s.Nickname = value; OnPropertyChanged(); PlayCommand.RaiseCanExecuteChanged(); }
    }

    public string GtaPath
    {
        get => _s.GtaPath;
        set { _s.GtaPath = value; OnPropertyChanged(); RefreshDerived(); }
    }

    public string AltvCoreDir
    {
        get => _s.AltvCoreDir;
        set { _s.AltvCoreDir = value; OnPropertyChanged(); RefreshDerived(); }
    }

    public bool AllowMultipleInstances
    {
        get => _s.AllowMultipleInstances;
        set { _s.AllowMultipleInstances = value; OnPropertyChanged(); }
    }

    public bool SyncAltvToml
    {
        get => _s.SyncAltvToml;
        set { _s.SyncAltvToml = value; OnPropertyChanged(); }
    }

    public string CoreManifestUrl
    {
        get => _s.CoreManifestUrl;
        set { _s.CoreManifestUrl = value; OnPropertyChanged(); SyncCoreCommand.RaiseCanExecuteChanged(); }
    }

    public string CompatManifestUrl
    {
        get => _s.CompatManifestUrl;
        set { _s.CompatManifestUrl = value; OnPropertyChanged(); }
    }

    // --- состояние синхронизации ядра ----------------------------

    private bool _isSyncing;
    public bool IsSyncing
    {
        get => _isSyncing;
        private set { if (SetField(ref _isSyncing, value)) { SyncCoreCommand.RaiseCanExecuteChanged(); PlayCommand.RaiseCanExecuteChanged(); } }
    }

    private double _syncFraction;
    public double SyncFraction { get => _syncFraction; private set => SetField(ref _syncFraction, value); }

    private string _syncStatusText = "";
    public string SyncStatusText { get => _syncStatusText; private set => SetField(ref _syncStatusText, value); }

    // --- производные (для UI) --------------------------------------

    private string _serverStatusText = "не проверено";
    public string ServerStatusText { get => _serverStatusText; private set => SetField(ref _serverStatusText, value); }

    private Brush _serverStatusBrush = Brushes.Gray;
    public Brush ServerStatusBrush { get => _serverStatusBrush; private set => SetField(ref _serverStatusBrush, value); }

    private string _gtaStatusText = "";
    public string GtaStatusText { get => _gtaStatusText; private set => SetField(ref _gtaStatusText, value); }

    private string _coreStatusText = "";
    public string CoreStatusText { get => _coreStatusText; private set => SetField(ref _coreStatusText, value); }

    private string _log = "";
    public string LogText { get => _log; private set => SetField(ref _log, value); }

    public bool CanPlay =>
        GtaLocator.LooksLikeGtaFolder(_s.GtaPath)
        && AltvClientCore.Validate(_s.AltvCoreDir).Ok
        && !string.IsNullOrWhiteSpace(_s.Nickname);

    // --- команды --------------------------------------------------

    public RelayCommand CheckServerCommand { get; }
    public RelayCommand DetectGtaCommand { get; }
    public RelayCommand BrowseGtaCommand { get; }
    public RelayCommand BrowseCoreCommand { get; }
    public RelayCommand SyncCoreCommand { get; }
    public RelayCommand PlayCommand { get; }
    public RelayCommand SaveCommand { get; }

    private async Task SyncCoreAsync()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        SyncFraction = 0;
        _syncCts = new CancellationTokenSource();

        var log = new Progress<string>(Log);
        var prog = new Progress<DownloadProgress>(p =>
        {
            SyncFraction = p.Fraction;
            SyncStatusText = $"{p.FilesDone}/{p.FilesTotal} · {p.DoneBytes / 1048576d:0.0}/{p.TotalBytes / 1048576d:0.0} МБ · {p.BytesPerSecond / 1048576d:0.0} МБ/с";
        });

        try
        {
            Save();
            Log($"Синхронизация ядра: {_s.CoreManifestUrl} → {_s.AltvCoreDir}");
            var report = await _sync.SyncAsync(_s.CoreManifestUrl, _s.AltvCoreDir, log, prog, _syncCts.Token);
            SyncStatusText = report.Message;
            Log("Итог: " + report.Message);
            if (report.Download?.Errors is { Count: > 0 } errs)
            {
                foreach (var e in errs.Take(10)) Log("  ! " + e);
            }
        }
        catch (OperationCanceledException)
        {
            Log("Синхронизация отменена.");
            SyncStatusText = "отменено";
        }
        catch (Exception ex)
        {
            Log("ОШИБКА синхронизации: " + ex.GetBaseException().Message);
            SyncStatusText = "ошибка";
        }
        finally
        {
            IsSyncing = false;
            RefreshDerived();
        }
    }

    private async Task CheckServerAsync()
    {
        ServerStatusText = "проверка…";
        ServerStatusBrush = Brushes.Gray;
        var r = await ServerStatus.CheckAsync(_s.ServerHost, _s.ServerPort);
        ServerStatusText = r.Text;
        ServerStatusBrush = r.State switch
        {
            ServerState.Online => new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)),
            ServerState.Offline => new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
            _ => Brushes.Gray,
        };
        Log($"Статус сервера {_s.ServerHost}:{_s.ServerPort} — {r.Text}");
    }

    private void DetectGta()
    {
        var list = GtaLocator.Detect();
        if (list.Count == 0)
        {
            Log("Автодетект GTA V: ничего не найдено. Укажите папку вручную.");
            return;
        }

        foreach (var c in list) Log($"Найдено: {c.Path}  [{c.Source}]");
        GtaPath = list[0].Path;
    }

    private void BrowseGta()
    {
        var dlg = new OpenFolderDialog { Title = "Папка GTA V (с GTA5.exe)" };
        if (!string.IsNullOrWhiteSpace(_s.GtaPath) && Directory.Exists(_s.GtaPath)) dlg.InitialDirectory = _s.GtaPath;
        if (dlg.ShowDialog() == true) GtaPath = dlg.FolderName;
    }

    private void BrowseCore()
    {
        var dlg = new OpenFolderDialog { Title = "Папка ядра клиента alt:V (с altv.exe)" };
        if (!string.IsNullOrWhiteSpace(_s.AltvCoreDir) && Directory.Exists(_s.AltvCoreDir)) dlg.InitialDirectory = _s.AltvCoreDir;
        if (dlg.ShowDialog() == true) AltvCoreDir = dlg.FolderName;
    }

    private async Task PlayAsync()
    {
        try
        {
            Save();

            if (!await CompatGateAsync())
                return;

            if (_s.SyncAltvToml)
            {
                AltvClientCore.SyncAltvToml(_s.GtaPath, _s.Branch, _s.Nickname);
                Log("altv.toml синхронизирован (.bak рядом).");
            }

            var url = AltvClientCore.BuildConnectUrl(_s.ServerHost, _s.ServerPort, _s.Nickname, null);
            var args = AltvClientCore.BuildArgs(url, _s.Branch, _s.AllowMultipleInstances);
            Log($"Запуск: altv.exe {args}");
            Log($"CWD: {_s.AltvCoreDir}");

            var proc = AltvClientCore.Launch(_s.AltvCoreDir, args);
            Log($"Процесс запущен, PID {proc.Id}. Дальше работает клиент alt:V.");
        }
        catch (Exception ex)
        {
            Log("ОШИБКА запуска: " + ex);
        }
    }

    /// <summary>
    /// Проверка совместимости версии GTA5.exe перед запуском. Возвращает
    /// false = запуск отменён (версия сломана). Предупреждения не блокируют.
    /// </summary>
    private async Task<bool> CompatGateAsync()
    {
        if (string.IsNullOrWhiteSpace(_s.CompatManifestUrl))
            return true;

        var game = GameVersion.Detect(_s.GtaPath);
        if (game is null)
        {
            Log("Совместимость: не удалось определить версию GTA5.exe — пропускаю проверку.");
            return true;
        }

        var res = await new CompatService().EvaluateAsync(_s.CompatManifestUrl, game);
        Log($"Совместимость [{res.Verdict}]: {res.Message}");

        switch (res.Verdict)
        {
            case CompatVerdict.NeedsFallback:
                Log("  → версия несовместима. Нужен Вариант 2 (подмена GTA5.exe) — пока не автоматизирован. Запуск отменён.");
                if (res.FallbackBuild is { } b)
                    Log($"  → эталонный билд для отката: {b.Id} ({b.GtaFileVersion}), url={b.Url}");
                return false;

            case CompatVerdict.Unknown:
                Log("  → версия игры новее манифеста. Запускаю, но клиент может не подключиться.");
                return true;

            default:
                return true;
        }
    }

    private void Save()
    {
        SettingsStore.Save(_s);
        Log("Настройки сохранены.");
    }

    // --- helpers -------------------------------------------------

    private void RefreshDerived()
    {
        GtaStatusText = GtaLocator.LooksLikeGtaFolder(_s.GtaPath)
            ? "GTA5.exe найден"
            : "GTA5.exe НЕ найден в этой папке";

        var v = AltvClientCore.Validate(_s.AltvCoreDir);
        CoreStatusText = v.Ok
            ? v.Summary
            : v.Summary + ": " + string.Join(", ", v.Missing.Take(6)) + (v.Missing.Count > 6 ? " …" : "");

        OnPropertyChanged(nameof(CanPlay));
        PlayCommand.RaiseCanExecuteChanged();
    }

    private void Log(string line)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}";
    }
}
