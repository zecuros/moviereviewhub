using System.Reactive.Linq;

namespace WatchlistService.Services;

public class MovieReactiveClient
{
    private readonly HttpClient _httpClient;

    public MovieReactiveClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public IObservable<bool> MovieExists(int movieId)
    {
        return Observable.FromAsync(async () =>
        {
            var response = await _httpClient.GetAsync($"api/movie/{movieId}");

            if (response.IsSuccessStatusCode)
                return true;

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();

            return false;
        });
    }
}