using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace E2E.Tests;

public class CommunicationTests
{
    [Fact]
    public async Task RegisterWatchlistAndReview_UsesReactiveLookupAndCreatesNotification()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("E2E_BASE_URL")
                ?? "http://localhost:5000")
        };
        var name = $"demo-{Guid.NewGuid():N}";
        var registration = await client.PostAsJsonAsync("/auth/register", new
        {
            username = name, email = $"{name}@example.test", password = Guid.NewGuid().ToString()
        });
        registration.EnsureSuccessStatusCode();
        var user = (await registration.Content.ReadFromJsonAsync<UserResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        (await client.GetAsync("/auth/profile")).EnsureSuccessStatusCode();

        var created = await client.PostAsJsonAsync("/movies", new
        {
            title = name, description = "Communication test", genre = "Test", releaseYear = 2026
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var movie = (await created.Content.ReadFromJsonAsync<IdResponse>())!;

        var added = await client.PostAsJsonAsync("/watchlist", new
        {
            userId = user.UserId, movieId = movie.Id, movieTitle = name
        });
        added.EnsureSuccessStatusCode();
        var missing = await client.PostAsJsonAsync("/watchlist", new
        {
            userId = user.UserId, movieId = int.MaxValue, movieTitle = "Missing movie"
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var review = await client.PostAsJsonAsync("/reviews", new
        {
            userId = user.UserId, movieId = movie.Id, username = name, rating = 4, comment = "Test"
        });
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);
        // RabbitMQ processing is asynchronous; wait for persisted notification, not a fixed delay.
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var notifications = await client.GetFromJsonAsync<List<NotificationResponse>>(
                $"/notifications/user/{user.UserId}");
            if (notifications?.Any(n => n.MovieId == movie.Id) == true)
                return;
            await Task.Delay(1000);
        }
        Assert.Fail("ReviewCreated did not produce a notification within 30 seconds.");
    }

    private record UserResponse(int UserId, string Token);
    private record IdResponse(int Id);
    private record NotificationResponse(int MovieId);
}
