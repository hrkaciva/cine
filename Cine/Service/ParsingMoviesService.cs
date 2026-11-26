using Cine.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cine.Service;

public class ParsingMoviesService(HttpClient httpClient, IMemoryCache cache, ILogger<ParsingMoviesService> logger)
{
    private const string CacheKey = "movies";
    private const string UpcomingCacheKey = "upcoming-movies";
    private const string StaleCacheKey = "movies-stale";
    private const string StaleUpcomingCacheKey = "upcoming-movies-stale";
    private const string HomepageUrl = "https://cinestarcinemas.ba/";
    private const string ScheduleUrl = "https://cinestarcinemas.ba/mostar-mepas-mall";

    public async Task<MovieDataResult> GetMoviesAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out CachedMovies? cachedMovies))
        {
            return new MovieDataResult(cachedMovies!.Movies, false, cachedMovies.FetchedAt, null);
        }

        return await GetOrUseStaleAsync(CacheKey, StaleCacheKey, ScheduleUrl, cancellationToken);
    }

    public async Task<MovieDataResult> GetUpcomingMoviesAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(UpcomingCacheKey, out CachedMovies? cachedMovies))
        {
            return new MovieDataResult(cachedMovies!.Movies, false, cachedMovies.FetchedAt, null);
        }

        try
        {
            var upcomingUrl = await FindUpcomingUrlAsync(cancellationToken);
            return await GetOrUseStaleAsync(UpcomingCacheKey, StaleUpcomingCacheKey, upcomingUrl, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return UseStaleOrThrow(StaleUpcomingCacheKey, exception);
        }
    }

    private async Task<MovieDataResult> GetOrUseStaleAsync(
        string cacheKey,
        string staleCacheKey,
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var movies = await ScrapeMoviesAsync(sourceUrl, cancellationToken);
            var cachedMovies = new CachedMovies(movies, DateTimeOffset.UtcNow);
            cache.Set(cacheKey, cachedMovies, TimeSpan.FromHours(1));
            cache.Set(staleCacheKey, cachedMovies);
            return new MovieDataResult(movies, false, cachedMovies.FetchedAt, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return UseStaleOrThrow(staleCacheKey, exception);
        }
    }

    private MovieDataResult UseStaleOrThrow(string staleCacheKey, Exception exception)
    {
        if (cache.TryGetValue(staleCacheKey, out CachedMovies? staleMovies))
        {
            logger.LogWarning(exception, "Using stale CineStar data after refresh failed");
            return new MovieDataResult(staleMovies!.Movies, true, staleMovies.FetchedAt, exception.Message);
        }

        logger.LogError(exception, "CineStar data could not be loaded and no cached data is available");
        throw exception;
    }

    private async Task<string> FindUpcomingUrlAsync(CancellationToken cancellationToken)
    {
        var htmlResponse = await httpClient.GetStringAsync(HomepageUrl, cancellationToken);
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

    private async Task<List<Movie>> ScrapeMoviesAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        logger.LogInformation("Scraping CineStar schedule from {SourceUrl}", sourceUrl);
        var htmlResponse = await httpClient.GetStringAsync(sourceUrl, cancellationToken);

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlResponse);

        var movies = new List<Movie>();
        var movieNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' movie-item ')]");

        foreach (var movieNode in movieNodes ?? Enumerable.Empty<HtmlNode>())
        {
            var titleNode = movieNode.SelectSingleNode(".//h2") ?? movieNode.SelectSingleNode(".//h3");
            var title = HtmlEntity.DeEntitize(titleNode?.InnerText ?? "Untitled movie").Trim();
            var showTimes = new List<ShowTime>();
            var detailLink = movieNode.SelectSingleNode(".//a[@href]")?.GetAttributeValue("href", "") ?? "";
            var description = FirstText(movieNode, ".//div[contains(@class, 'description')]")
                              ?? FirstText(movieNode, ".//p");
            var price = FirstText(movieNode, ".//*[contains(@class, 'price')]") ?? "";
            var releaseDate = ParseReleaseDate(
                FirstText(movieNode, ".//div[contains(concat(' ', normalize-space(@class), ' '), ' najava ')]"));
            var searchableText = movieNode.InnerText.ToLowerInvariant();

            if (searchableText.Contains("uživo") || searchableText.Contains("uzivo"))
                continue;

            var dayWrappers = movieNode.SelectNodes(".//div[contains(concat(' ', normalize-space(@class), ' '), ' day-wrapper ')]");

            if (dayWrappers != null)
            {
                foreach (var dayWrapper in dayWrappers)
                {
                    var dayText = dayWrapper.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' day ')]")?.InnerText.Trim();

                    var date = ParseDate(dayText);

                    var timeNodes = dayWrapper.SelectNodes(".//div[contains(concat(' ', normalize-space(@class), ' '), ' time ')]");

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
                ReleaseDate = releaseDate,
                Showtimes = showTimes
            });
        }

        if (movies.Count == 0)
            throw new InvalidOperationException("CineStar returned no movie entries. The source markup may have changed.");

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

    private static DateOnly? ParseReleaseDate(string? dateText)
    {
        if (string.IsNullOrEmpty(dateText))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(dateText, @"\d{2}\.\d{2}\.\d{4}");

        return DateOnly.TryParseExact(match.Value, "dd.MM.yyyy", out var date) ? date : null;
    }

    private static DateOnly? ParseDate(string? dateText)
    {
        if (string.IsNullOrEmpty(dateText))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(dateText, @"\d{2}\.\d{2}\.");

        if (!match.Success)
            return null;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var possibleDates = new[] { today.Year - 1, today.Year, today.Year + 1 }
            .Select(year => DateOnly.TryParseExact(match.Value + year, "dd.MM.yyyy", out var date) ? date : (DateOnly?)null)
            .Where(date => date.HasValue)
            .Select(date => date!.Value);

        return possibleDates.OrderBy(date => Math.Abs(date.DayNumber - today.DayNumber)).FirstOrDefault();
    }

    public async Task<List<Movie>> GetMovieByDateAsync(DateOnly? selectedDate)
    {
        var movies = (await GetMoviesAsync()).Movies;
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
                    ReleaseDate = movie.ReleaseDate,
                    Showtimes = filteredShowTimes
                });
            }
        }

        return filteredMovies;
    }

    public async Task<List<DateOnly>> GetMovieDatesAsync()
    {
        var movies = (await GetMoviesAsync()).Movies;
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

    private sealed record CachedMovies(List<Movie> Movies, DateTimeOffset FetchedAt);
}

public sealed record MovieDataResult(
    List<Movie> Movies,
    bool IsStale,
    DateTimeOffset FetchedAt,
    string? ErrorMessage);
