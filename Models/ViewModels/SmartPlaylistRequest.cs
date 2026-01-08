namespace MusicWeb.Models.ViewModels;

/// <summary>Request từ user để tạo AI playlist</summary>
public class SmartPlaylistRequest
{
    /// <summary>Prompt tự nhiên từ user, VD: "Nhạc EDM của Sơn Tùng"</summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>Request confirm tạo playlist từ preview</summary>
public class CreateAIPlaylistRequest
{
    public string PlaylistName { get; set; } = string.Empty;
    public List<int> SongIds { get; set; } = new();
}
