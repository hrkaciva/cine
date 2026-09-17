using Cine.Service;
using Microsoft.Extensions.Caching.Memory;

var httpClient = new HttpClient();
var cache = new MemoryCache(new MemoryCacheOptions());
var service = new ParsingMoviesService(httpClient, cache);
var movies = await service.GetMoviesAsync();

foreach (var movie in movies)
{
    Console.WriteLine($"{movie.Title}: {string.Join(", ", movie.Showtimes)}");
}