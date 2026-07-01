namespace ReviewService.Models;

public class Review
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}