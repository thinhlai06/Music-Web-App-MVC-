using Microsoft.AspNetCore.Identity;

namespace MusicWeb.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
    public ICollection<FavoriteSong> FavoriteSongs { get; set; } = new List<FavoriteSong>();
    public ICollection<PlayHistory> PlayHistories { get; set; } = new List<PlayHistory>();
    public ICollection<UserSongRating> SongRatings { get; set; } = new List<UserSongRating>();
}

