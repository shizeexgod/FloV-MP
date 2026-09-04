using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloridaV.Launcher.Models;
using FloridaV.Launcher.Services;
using Microsoft.Win32;

namespace FloridaV.Launcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LauncherSettings _settings;
    private CancellationTokenSource? _statusCts;

    public MainViewModel()
    {
        _settings = LauncherSettingsService.Load();
        _nickname = _settings.Nickname;
        _gtaPath = _settings.GtaPath;
        _serverHost = _settings.ServerHost;
        _serverPort = _settings.ServerPort;
        _clientEdition = _settings.ClientEdition;
        _autoUpdate = _settings.AutoUpdate;

        // Если есть сохранённый ник - пропускаем авторизацию
        _isAuthenticated = !string.IsNullOrEmpty(_nickname) && _nickname != "Игрок";
        _authLogin = _isAuthenticated ? _nickname : "";

        if (string.IsNullOrEmpty(_gtaPath))
            _ = Task.Run(AutoDetectGtaAsync);

        StartStatusPolling();
    }

    // ─── Данные игрока ──────────────────────────────────────────────────────
    [ObservableProperty] private string _nickname;
    [ObservableProperty] private string _gtaPath = "";
    [ObservableProperty] private string _serverHost = "127.0.0.1";
    [ObservableProperty] private int _serverPort = 7788;
    [ObservableProperty] private string _gtaVersion = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _hasError = false;

    public string VersionText => "Florida V Launcher v1.0.0  |  FloV:MP";
    public string NicknameInitial => Nickname.Length > 0 ? Nickname[..1].ToUpper() : "Г";

    // ─── Навигация ──────────────────────────────────────────────────────────
    [ObservableProperty] private AppPage _currentPage = AppPage.Play;

    // ─── Настройки (страница «Настройки») ──────────────────────────────────
    [ObservableProperty] private string _clientEdition = "Legacy";
    [ObservableProperty] private bool _autoUpdate = true;
    [ObservableProperty] private string _settingsSavedMessage = "";

    public bool IsLegacyEdition => ClientEdition == "Legacy";
    public bool IsEnhancedEdition => ClientEdition == "Enhanced";

    // ─── Авторизация в лаунчере ─────────────────────────────────────────────
    [ObservableProperty] private bool _isAuthenticated = false;
    [ObservableProperty] private bool _isRegisterMode = false;
    [ObservableProperty] private string _authLogin = "";
    [ObservableProperty] private string _authPassword = "";
    [ObservableProperty] private string _authError = "";

    public bool HasAuthError => !string.IsNullOrEmpty(AuthError);
    public string AuthButtonText => IsRegisterMode ? "ЗАРЕГИСТРИРОВАТЬСЯ" : "ВОЙТИ";

    [RelayCommand]
    private void ToggleRegisterMode()
    {
        IsRegisterMode = !IsRegisterMode;
        AuthError = "";
        OnPropertyChanged(nameof(AuthButtonText));
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        AuthError = "";
        if (string.IsNullOrWhiteSpace(AuthLogin) || string.IsNullOrWhiteSpace(AuthPassword))
        {
            AuthError = "Введите логин и пароль";
            OnPropertyChanged(nameof(HasAuthError));
            return;
        }

        // TODO: Здесь будет реальный запрос
        await Task.Delay(500);

        if (IsRegisterMode && AuthPassword.Length < 6)
        {
            AuthError = "Пароль слишком короткий (минимум 6 символов)";
            OnPropertyChanged(nameof(HasAuthError));
            return;
        }

        IsAuthenticated = true;
        Nickname = AuthLogin;
        SaveSettings();
    }

    // ─── Статус сервера ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _serverOnline = false;
    [ObservableProperty] private int _playersOnline = 0;

    public string ServerStatusText => ServerOnline ? "Онлайн" : "Офлайн";
    public string OnlineText => ServerOnline ? $"{PlayersOnline} игроков" : "Нет данных";
    public Brush ServerStatusBrush => ServerOnline
        ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
        : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

    // ─── Кнопка ИГРАТЬ ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLaunching = false;
    [ObservableProperty] private bool _isUpdating = false;
    [ObservableProperty] private double _updateProgress = 0;
    [ObservableProperty] private string _updateStatusText = "";

    public string PlayButtonText => IsLaunching ? "ЗАПУСК..." : "ИГРАТЬ";

    public ObservableCollection<NewsItem> NewsItems { get; } = new()
    {
        new NewsItem { Title = "Открытие Держава RP — добро пожаловать!", Date = "30.08.2026" },
        new NewsItem { Title = "Новая карта: реальные улицы Москвы", Date = "29.08.2026" },
        new NewsItem { Title = "Обновление FloV:MP 1.0 — стабильный запуск", Date = "28.08.2026" },
        new NewsItem { Title = "Первые RP-фракции открыты для вступления", Date = "27.08.2026" },
    };

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (string.IsNullOrWhiteSpace(GtaPath) || !GtaLocatorService.IsValidGtaFolder(GtaPath))
        {
            ShowError("Папка GTA V не найдена. Укажи путь в настройках.");
            return;
        }

        HasError = false;
        IsLaunching = true;
        StatusMessage = "Запуск игры...";
        OnPropertyChanged(nameof(PlayButtonText));

        var result = await Task.Run(() => PlayService.Launch(GtaPath, ServerHost, ServerPort, Nickname));

        IsLaunching = false;
        OnPropertyChanged(nameof(PlayButtonText));

        if (result.Success)
            StatusMessage = "Игра запущена! Хорошей игры!";
        else
            ShowError(result.Error ?? "Неизвестная ошибка запуска.");
    }

    [RelayCommand]
    private void DetectGta() => _ = Task.Run(AutoDetectGtaAsync);
    [RelayCommand]
    private void NavigatePlay() => CurrentPage = AppPage.Play;
    [RelayCommand]
    private void NavigateNews() => CurrentPage = AppPage.News;
    [RelayCommand]
    private void NavigateSettings() => CurrentPage = AppPage.Settings;
    [RelayCommand]
    private void OpenDiscord() => OpenUrl("https://discord.gg/floridav");
    [RelayCommand]
    private void OpenForum() => OpenUrl("https://forum.florida-v.ru");
    [RelayCommand]
    private void OpenDonate() => OpenUrl("https://donate.florida-v.ru");

    [RelayCommand]
    private void BrowseGtaPath()
    {
        var dlg = new OpenFolderDialog { Title = "Папка с GTA5.exe / GTA5_Enhanced.exe" };
        if (!string.IsNullOrWhiteSpace(GtaPath) && Directory.Exists(GtaPath))
            dlg.InitialDirectory = GtaPath;

        if (dlg.ShowDialog() != true) return;

        if (!GtaLocatorService.IsValidGtaFolder(dlg.FolderName))
        {
            ShowError("В выбранной папке не найден GTA5.exe / GTA5_Enhanced.exe.");
            return;
        }

        GtaPath = dlg.FolderName;
        GtaVersion = GtaLocatorService.DetectVersion(GtaPath);
        ClientEdition = GtaVersion == "Enhanced" ? "Enhanced" : "Legacy";
        HasError = false;
        StatusMessage = $"GTA V указана вручную — {GtaVersion}";
    }

    /// <summary>Параметр — "Legacy" или "Enhanced".</summary>
    [RelayCommand]
    private void SetClientEdition(string edition) => ClientEdition = edition;

    private Task AutoDetectGtaAsync()
    {
        var found = GtaLocatorService.TryLocate();
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (found.HasValue)
            {
                GtaPath = found.Value.Path;
                GtaVersion = GtaLocatorService.DetectVersion(GtaPath);
                StatusMessage = $"GTA V найдена: {found.Value.Source} — {GtaVersion}";
                HasError = false;
                SaveSettings();
            }
            else
            {
                ShowError("GTA V не найдена автоматически. Укажи путь в настройках.");
            }
        });
        return Task.CompletedTask;
    }

    private void StartStatusPolling()
    {
        _statusCts = new CancellationTokenSource();
        var ct = _statusCts.Token;
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await ServerStatusService.CheckAsync(ServerHost, ServerPort);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ServerOnline = result.Online;
                    PlayersOnline = result.Players;
                    OnPropertyChanged(nameof(ServerStatusText));
                    OnPropertyChanged(nameof(OnlineText));
                    OnPropertyChanged(nameof(ServerStatusBrush));
                });
                try { await Task.Delay(10_000, ct); } catch (OperationCanceledException) { break; }
            }
        }, ct);
    }

    private void SaveSettings()
    {
        _settings.Nickname = Nickname;
        _settings.GtaPath = GtaPath;
        _settings.ServerHost = ServerHost;
        _settings.ServerPort = ServerPort;
        _settings.ClientEdition = ClientEdition;
        _settings.AutoUpdate = AutoUpdate;
        LauncherSettingsService.Save(_settings);
    }

    private void ShowError(string message)
    {
        HasError = true;
        StatusMessage = message;
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    partial void OnNicknameChanged(string value) => SaveSettings();
    partial void OnGtaPathChanged(string value) => SaveSettings();
    partial void OnServerHostChanged(string value) => SaveSettings();
    partial void OnServerPortChanged(int value) => SaveSettings();
    partial void OnAutoUpdateChanged(bool value) => SaveSettings();

    partial void OnClientEditionChanged(string value)
    {
        SaveSettings();
        OnPropertyChanged(nameof(IsLegacyEdition));
        OnPropertyChanged(nameof(IsEnhancedEdition));
        SettingsSavedMessage = $"Профиль клиента: {value}";
    }
}
