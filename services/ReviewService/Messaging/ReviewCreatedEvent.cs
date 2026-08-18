namespace ReviewService.Messaging;

public class ReviewCreatedEvent
{
    public int ReviewId { get; set; }
    public int MovieId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Rating { get; set; }
}