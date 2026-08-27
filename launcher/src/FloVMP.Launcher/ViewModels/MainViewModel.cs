using System.IO;
using System.Windows.Media;
using FloVMP.Launcher.Models;
using FloVMP.Launcher.Mvvm;
using FloVMP.Launcher.Services;
using Microsoft.Win32;

namespace FloVMP.Launcher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly LauncherSettings _s;

    public MainViewModel()
    {
        _s = SettingsStore.Load();

        CheckServerCommand = new RelayCommand(async () => await CheckServerAsync());
        DetectGtaCommand = new RelayCommand(DetectGta);
        BrowseGtaCommand = new RelayCommand(BrowseGta);
        BrowseCoreCommand = new RelayCommand(BrowseCore);
        PlayCommand = new RelayCommand(Play, () => CanPlay);
        SaveCommand = new RelayCommand(Save);

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
    public RelayCommand PlayCommand { get; }
    public RelayCommand SaveCommand { get; }

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

    private void Play()
    {
        try
        {
            Save();

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
