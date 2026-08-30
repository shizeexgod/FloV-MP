namespace FloridaV.Launcher.Models;

public class ServerInfo
{
    public bool IsOnline { get; set; }
    public int PlayersOnline { get; set; }
    public int MaxPlayers { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
}
