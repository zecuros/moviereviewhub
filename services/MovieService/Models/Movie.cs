namespace MovieService.Models;

public class Movie
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public int ReleaseYear { get; set; }

    public double AverageRating { get; set; }
}