namespace WatchlistService.Models;

public class WatchlistItem
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}