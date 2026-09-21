using Cine.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;

namespace Cine.Service;

public class ParsingMoviesService(HttpClient httpClient, IMemoryCache cache)
{
    private const string CacheKey = "movies";
    private const string UpcomingCacheKey = "upcoming-movies";
    private const string HomepageUrl = "https://cinestarcinemas.ba/";
    private const string ScheduleUrl = "https://cinestarcinemas.ba/mostar-mepas-mall";

    public async Task<List<Movie>> GetMoviesAsync()
    {
        if (cache.TryGetValue(CacheKey, out List<Movie>? cachedMovies))
        {
            return cachedMovies!;
        }

        var movies = await ScrapeMoviesAsync(ScheduleUrl);

        cache.Set(CacheKey, movies, TimeSpan.FromHours(1));

        return movies;
    }

    public async Task<List<Movie>> GetUpcomingMoviesAsync()
    {
        if (cache.TryGetValue(UpcomingCacheKey, out List<Movie>? cachedMovies))
        {
            return cachedMovies!;
        }

        var upcomingUrl = await FindUpcomingUrlAsync();
        var movies = await ScrapeMoviesAsync(upcomingUrl);
        cache.Set(UpcomingCacheKey, movies, TimeSpan.FromHours(1));

        return movies;
    }

    private async Task<string> FindUpcomingUrlAsync()
    {
        var htmlResponse = await httpClient.GetStringAsync(HomepageUrl);
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlResponse);

        var upcomingLink = htmlDoc.DocumentNode
            .SelectNodes("//a[@href]")?
            .FirstOrDefault(link => HtmlEntity.DeEntitize(link.InnerText).Trim()
                .Contains("uskoro", StringComparison.OrdinalIgnoreCase));

        if (upcomingLink == null)
            throw new InvalidOperationException("Could not find the upcoming movies link on the CineStar homepage.");

        return MakeAbsoluteUrl(upcomingLink.GetAttributeValue("href", ""));
    }

    private async Task<List<Movie>> ScrapeMoviesAsync(string sourceUrl)
    {
        Console.WriteLine($"Scraping CineStar... {DateTime.Now}");
        var htmlResponse = await httpClient.GetStringAsync(sourceUrl);

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlResponse);

        var movies = new List<Movie>();
        var movieNodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='movie-item d-flex']");

        foreach (var movieNode in movieNodes)
        {
            var titleNode = movieNode.SelectSingleNode(".//h2") ?? movieNode.SelectSingleNode(".//h3");
            var title = HtmlEntity.DeEntitize(titleNode?.InnerText ?? "Untitled movie").Trim();
            var showTimes = new List<ShowTime>();
            var detailLink = movieNode.SelectSingleNode(".//a[@href]")?.GetAttributeValue("href", "") ?? "";
            var description = FirstText(movieNode, ".//div[contains(@class, 'description')]")
                              ?? FirstText(movieNode, ".//p");
            var price = FirstText(movieNode, ".//*[contains(@class, 'price')]") ?? "";
            var searchableText = movieNode.InnerText.ToLowerInvariant();

            var dayWrappers = movieNode.SelectNodes(".//div[@class='day-wrapper']");

            if (dayWrappers != null)
            {
                foreach (var dayWrapper in dayWrappers)
                {
                    var dayText = dayWrapper.SelectSingleNode(".//div[@class='day']")?.InnerText.Trim();

                    var date = ParseDate(dayText);

                    var timeNodes = dayWrapper.SelectNodes(".//div[@class='time']");

                    if (timeNodes != null && date.HasValue)
                    {
                        foreach (var timeNode in timeNodes)
                        {
                            showTimes.Add(
                                new ShowTime
                                {
                                    Date = date.Value,
                                    Time = HtmlEntity.DeEntitize(timeNode.InnerText).Trim()
                                });
                        }
                    }
                }
            }

            movies.Add(new Movie
            {
                Title = title,
                Description = description ?? "A new story on the big screen.",
                Price = price,
                DetailUrl = MakeAbsoluteUrl(detailLink),
                IsCartoon = movieNode.GetAttributeValue("class", "").Contains("cartoon", StringComparison.OrdinalIgnoreCase)
                            || searchableText.Contains("animirani")
                            || searchableText.Contains("animation"),
                Showtimes = showTimes
            });
        }

        return movies;
    }

    private static string? FirstText(HtmlNode node, string selector)
    {
        var selected = node.SelectSingleNode(selector);
        return selected == null ? null : HtmlEntity.DeEntitize(selected.InnerText).Trim();
    }

    private static string MakeAbsoluteUrl(string url) =>
        string.IsNullOrWhiteSpace(url) ? "https://cinestarcinemas.ba/mostar-mepas-mall" :
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://cinestarcinemas.ba{(url.StartsWith('/') ? "" : "/")}{url}";

    private static DateOnly? ParseDate(string? dateText)
    {
        if (string.IsNullOrEmpty(dateText))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(dateText, @"\d{2}\.\d{2}\.");

        if (!match.Success)
            return null;

        var dateString = match.Value + DateTime.Now.Year;

        if (DateOnly.TryParseExact(dateString, "dd.MM.yyyy", out var date))
            return date;

        return null;
    }

    public async Task<List<Movie>> GetMovieByDateAsync(DateOnly? selectedDate)
    {
        var movies = await GetMoviesAsync();
        var filteredMovies = new List<Movie>();

        foreach (var movie in movies)
        {
            var filteredShowTimes = new List<ShowTime>();

            foreach (var showTime in movie.Showtimes)
            {
                if (showTime.Date == selectedDate)
                {
                    filteredShowTimes.Add(showTime);
                }
            }

            if (filteredShowTimes.Count > 0)
            {
                filteredMovies.Add(new Movie
                {
                    Title = movie.Title,
                    Description = movie.Description,
                    Price = movie.Price,
                    DetailUrl = movie.DetailUrl,
                    IsCartoon = movie.IsCartoon,
                    Showtimes = filteredShowTimes
                });
            }
        }

        return filteredMovies;
    }

    public async Task<List<DateOnly>> GetMovieDatesAsync()
    {
        var movies = await GetMoviesAsync();
        var dates = new List<DateOnly>();

        foreach (var movie in movies)
        {
            foreach (var showTime in movie.Showtimes)
            {
                if (!dates.Contains(showTime.Date))
                {
                    dates.Add(showTime.Date);
                }
            }
        }

        dates.Sort();
        return dates;
    }
}
