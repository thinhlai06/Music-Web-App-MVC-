namespace MusicWeb.Models.Entities;

public class EarningsHistory
{
    public int Id { get; set; }
    public string UploaderUserId { get; set; } = string.Empty;
    public ApplicationUser Uploader { get; set; } = null!;
    public string ListenerUserId { get; set; } = string.Empty;
    public ApplicationUser Listener { get; set; } = null!;
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
