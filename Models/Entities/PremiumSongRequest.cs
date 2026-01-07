namespace MusicWeb.Models.Entities;

public class PremiumSongRequest
{
    public int Id { get; set; }
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
    public string RequestedByUserId { get; set; } = string.Empty;
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? AdminNote { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewedByAdminId { get; set; }
}
