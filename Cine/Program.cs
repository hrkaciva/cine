using Cine.Service;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

var httpClient = new HttpClient();
var cache = new MemoryCache(new MemoryCacheOptions());
var service = new ParsingMoviesService(httpClient, cache, NullLogger<ParsingMoviesService>.Instance);
var movies = (await service.GetMoviesAsync()).Movies;

foreach (var movie in movies)
{
    Console.WriteLine($"{movie.Title}: {string.Join(", ", movie.Showtimes)}");
}
