using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;

namespace MusicWeb.Services;

public interface IAIPlaylistService
{
    /// <summary>Parse prompt và query database, trả về preview</summary>
    Task<SmartPlaylistResult> GeneratePreviewAsync(string prompt, string userId);
    
    /// <summary>Tạo playlist thật từ danh sách song IDs đã chọn</summary>
    Task<Playlist?> CreatePlaylistFromPreviewAsync(
        string playlistName, 
        List<int> songIds, 
        string userId);
}
