namespace MusicWeb.Models.Entities;

public class UserWallet
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public decimal Balance { get; set; } = 100000; // Default 100,000 VND test balance
    public decimal TotalEarnings { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
