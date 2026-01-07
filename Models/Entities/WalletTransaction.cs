namespace MusicWeb.Models.Entities;

public class WalletTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty; // Deposit, Purchase, Earning, Withdraw
    public string? Description { get; set; }
    public string? ReferenceId { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
