using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloridaV.Launcher.Models;

namespace FloridaV.Launcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // ─── Данные игрока ──────────────────────────────────────────────────────
    [ObservableProperty] private string _nickname = "Гость";
    [ObservableProperty] private string _versionText = "Florida V Launcher v1.0.0 | FloV:MP";

    public string NicknameInitial => Nickname.Length > 0 ? Nickname[..1].ToUpper() : "Г";

    // ─── Статус сервера ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _serverOnline = false;
    [ObservableProperty] private int _playersOnline = 0;
    
    public string ServerStatusText => ServerOnline ? "Онлайн" : "Офлайн";
    public string OnlineText => ServerOnline ? $"{PlayersOnline} игроков" : "Нет данных";
    public Brush ServerStatusBrush => ServerOnline
        ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
        : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

    // ─── Кнопка ИГРАТЬ ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _isUpdating = false;
    [ObservableProperty] private double _updateProgress = 0;
    [ObservableProperty] private string _updateStatusText = "";

    public string PlayButtonText => IsUpdating ? "ЗАГРУЖАЕТСЯ..." : "ИГРАТЬ";
    public bool CanPlay => !IsUpdating;

    // ─── Новости ────────────────────────────────────────────────────────────
    public ObservableCollection<NewsItem> NewsItems { get; } = new()
    {
        new NewsItem { Title = "Открытие Florida V — добро пожаловать!", Date = "30.08.2026" },
        new NewsItem { Title = "Новая карта: реальные улицы Москвы", Date = "29.08.2026" },
        new NewsItem { Title = "Обновление FloV:MP 1.0 — стабильный запуск", Date = "28.08.2026" },
        new NewsItem { Title = "Первые RP-фракции открыты для вступления", Date = "27.08.2026" },
    };

    // ─── Команды ────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task PlayAsync()
    {
        // TODO: Запустить FloVMP.Connect с нужными аргументами
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void NavigatePlay() { }

    [RelayCommand]
    private void NavigateNews() { }

    [RelayCommand]
    private void NavigateSettings() { }

    [RelayCommand]
    private void OpenDiscord()
        => OpenUrl("https://discord.gg/floridav");

    [RelayCommand]
    private void OpenForum()
        => OpenUrl("https://forum.florida-v.ru");

    [RelayCommand]
    private void OpenDonate()
        => OpenUrl("https://donate.florida-v.ru");

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}
