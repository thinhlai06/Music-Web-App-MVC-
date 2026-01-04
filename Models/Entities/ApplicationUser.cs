using Microsoft.AspNetCore.Identity;

namespace MusicWeb.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Provider { get; set; }  // "Google", "Facebook", "Local"
    public string? ProviderKey { get; set; }  // External ID from provider
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
    public ICollection<FavoriteSong> FavoriteSongs { get; set; } = new List<FavoriteSong>();
    public ICollection<PlayHistory> PlayHistories { get; set; } = new List<PlayHistory>();
    public ICollection<UserSongRating> SongRatings { get; set; } = new List<UserSongRating>();
    
    public ICollection<UserFollow> Followers { get; set; } = new List<UserFollow>();
    public ICollection<UserFollow> Following { get; set; } = new List<UserFollow>();
    public ICollection<UserAlbum> UserAlbums { get; set; } = new List<UserAlbum>();
}

