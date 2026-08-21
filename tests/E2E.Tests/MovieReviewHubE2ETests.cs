using System.Net;
using System.Net.Http.Json;

namespace E2E.Tests;

public class MovieReviewHubE2ETests
{
    private readonly HttpClient _client;

    public MovieReviewHubE2ETests()
    {
        var baseUrl =
            Environment.GetEnvironmentVariable("E2E_BASE_URL")
            ?? "http://localhost:5000";

        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    [Fact]
    public async Task MoviesEndpoint_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/movies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatingMovie_ThenGettingMovies_ReturnsCreatedMovie()
    {
        var uniqueTitle = $"E2E Movie {Guid.NewGuid()}";

        var movie = new
        {
            id = 0,
            title = uniqueTitle,
            description = "Created by E2E test",
            genre = "Test",
            releaseYear = 2026,
            averageRating = 0
        };

        var createResponse =
            await _client.PostAsJsonAsync("/movies", movie);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var movies =
            await _client.GetFromJsonAsync<List<MovieResponse>>("/movies");

        Assert.NotNull(movies);

        Assert.Contains(
            movies,
            movieItem => movieItem.Title == uniqueTitle);
    }

    private class MovieResponse
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public int ReleaseYear { get; set; }

        public double AverageRating { get; set; }
    }
}