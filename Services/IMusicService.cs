using MusicWeb.Models.ViewModels;
using MusicWeb.Models.Entities;

namespace MusicWeb.Services;

public interface IMusicService
{
    Task<HomeViewModel> BuildHomeAsync(string? userId);
    Task<SearchResultsViewModel> SearchAsync(string term, string? userId);
    Task<(IEnumerable<string> Lyrics, string SongTitle, string Artist)> GetLyricsAsync(int songId);
    Task<List<SongCardViewModel>> GetSongsByGenreAsync(int genreId, string userId);
    Task<bool> ToggleFavoriteAsync(int songId, string userId);
    Task<Playlist> CreatePlaylistAsync(string name, string userId);
    Task<bool> AddSongToPlaylistAsync(int playlistId, int songId, string userId);
    Task<bool> RemoveSongFromPlaylistAsync(int playlistId, int songId, string userId);
    Task<bool> DeletePlaylistAsync(int playlistId, string userId);
    Task RecordPlayAsync(int songId, string? userId);
    Task<AlbumDetailViewModel?> GetAlbumDetailAsync(int id, string? userId);
    Task<PlaylistDetailViewModel?> GetPlaylistDetailAsync(int id, string? userId);
    Task<GenreDetailViewModel?> GetGenreDetailAsync(int id, string? userId);
    Task<bool> UpdatePlaylistAsync(int id, string name, string? coverUrl, string userId);
    Task<bool> SetUserSongRatingAsync(int songId, decimal rating, string userId);
}
