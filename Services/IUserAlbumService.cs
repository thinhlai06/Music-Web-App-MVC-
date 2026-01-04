using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;

namespace MusicWeb.Services;

public interface IUserAlbumService
{
    Task<UserAlbum?> CreateAlbumAsync(string userId, string name, bool isPublic, string? description, IFormFile? coverFile, List<int>? songIds);
    Task<UserAlbumDetailViewModel?> GetAlbumByIdAsync(int albumId, string? userId);
    Task<List<UserAlbumCardViewModel>> GetUserAlbumsAsync(string userId);
    Task<bool> UpdateAlbumAsync(int albumId, string userId, string name, bool isPublic, string? description, IFormFile? coverFile);
    Task<bool> DeleteAlbumAsync(int albumId, string userId);
    Task<bool> AddSongToAlbumAsync(int albumId, string userId, int songId);
    Task<bool> AddNewSongToAlbumAsync(int albumId, string userId, IFormFile audioFile, IFormFile? coverFile, string title, string? description);
    Task<bool> RemoveSongFromAlbumAsync(int albumId, string userId, int songId, bool deleteSongFile);
}
