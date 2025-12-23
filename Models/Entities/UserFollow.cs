using System.ComponentModel.DataAnnotations.Schema;

namespace MusicWeb.Models.Entities;

public class UserFollow
{
    public string FollowerId { get; set; } = null!;
    public ApplicationUser Follower { get; set; } = null!;

    public string FolloweeId { get; set; } = null!;
    public ApplicationUser Followee { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
