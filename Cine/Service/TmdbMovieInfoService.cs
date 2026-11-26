using System.Text.Json;
using System.Text.RegularExpressions;
using Cine.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cine.Service;

/// <summary>
/// Enriches the CineStar schedule with details from TMDB: a score, a fuller description,
/// a couple of short review quotes and links to the movie's IMDb and Letterboxd pages.
/// Lookups are cached per title, and any failure simply leaves a movie unenriched.
/// </summary>
public partial class TmdbMovieInfoService(HttpClient httpClient, IMemoryCache cache, string apiKey, ILogger<TmdbMovieInfoService> logger)
{
    public const string ClientName = "tmdb";

    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissCacheDuration = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // TMDB fields are snake_case (vote_average, imdb_id, ...), not the camelCase Web default.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>Attaches TMDB details to every movie that does not already have them.</summary>
    public async Task EnrichMoviesAsync(IEnumerable<Movie> movies, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.WhenAll(movies.Select(movie => EnrichMovieAsync(movie, cancellationToken)));
        }
        catch (Exception exception)
        {
            // Enrichment is strictly optional; it must never take the schedule down.
            logger.LogWarning(exception, "TMDB enrichment failed; showing movies without extra details");
        }
    }

    /// <summary>Returns TMDB details for a title, or null when no confident match exists.</summary>
    public async Task<MovieInfo?> GetMovieInfoAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug("TMDB API key is not configured; movie enrichment is skipped");
            return null;
        }

        if (string.IsNullOrWhiteSpace(title))
            return null;

        var cacheKey = $"tmdb:{NormalizeTitle(title)}";

        if (cache.TryGetValue(cacheKey, out MovieInfo? cachedInfo))
            return cachedInfo;

        MovieInfo? info;

        try
        {
            info = await FindMovieInfoAsync(title, cancellationToken);
            cache.Set(cacheKey, info, info is null ? MissCacheDuration : SuccessCacheDuration);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // Transport failures are not cached, so the lookup is retried on the next request.
            logger.LogWarning(exception, "TMDB lookup failed for {MovieTitle}; leaving the movie unenriched", title);
            info = null;
        }

        return info;
    }

    private async Task<MovieInfo?> FindMovieInfoAsync(string title, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString(BuildSearchQuery(title));
        var search = await GetJsonAsync<TmdbSearch>($"search/movie?query={query}&include_adult=false&language=en-US", cancellationToken);

        if (search?.Results is not { Count: > 0 })
            return null;

        var normalizedTitle = NormalizeTitle(title);
        var match = search.Results.FirstOrDefault(candidate =>
                      NormalizeTitle(candidate.Title) == normalizedTitle ||
                      NormalizeTitle(candidate.OriginalTitle) == normalizedTitle)
                  ?? search.Results
                      .Where(candidate => candidate.Popularity >= 1)
                      .OrderByDescending(candidate => candidate.Popularity)
                      .FirstOrDefault();

        if (match is null)
            return null;

        var reviewsTask = GetJsonAsync<TmdbReviewList>($"movie/{match.Id}/reviews?language=en-US&page=1", cancellationToken);
        var externalIdsTask = GetJsonAsync<TmdbExternalIds>($"movie/{match.Id}/external_ids", cancellationToken);
        var reviews = await reviewsTask;
        var externalIds = await externalIdsTask;

        var imdbId = externalIds?.ImdbId;

        return new MovieInfo
        {
            Score = match.VoteAverage > 0 ? match.VoteAverage : null,
            Overview = match.Overview ?? string.Empty,
            ImdbUrl = string.IsNullOrWhiteSpace(imdbId) ? string.Empty : $"https://www.imdb.com/title/{imdbId}/",
            LetterboxdUrl = string.IsNullOrWhiteSpace(imdbId) ? string.Empty : $"https://letterboxd.com/imdb/{imdbId}/",
            Reviews = CollectQuotes(reviews?.Results)
        };
    }

    private async Task<MovieInfo?> EnrichMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        movie.Info ??= await GetMovieInfoAsync(movie.Title, cancellationToken);
        return movie.Info;
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var separator = relativeUrl.Contains('?') ? '&' : '?';
        var url = $"{relativeUrl}{separator}api_key={Uri.EscapeDataString(apiKey)}";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static List<MovieReview> CollectQuotes(List<TmdbReview>? reviews)
    {
        return reviews?
            .Where(review => !string.IsNullOrWhiteSpace(review.Author)
                              && !string.IsNullOrWhiteSpace(review.Content)
                              && review.Content.Length >= 80)
            .Take(3)
            .Select(review => new MovieReview
            {
                Author = review.Author,
                Quote = TrimText(review.Content, 220),
                Content = TrimText(review.Content, 900)
            })
            .ToList()
            ?? [];
    }

    /// <summary>Shortens review text to roughly <paramref name="limit"/> characters, preferring a sentence end.</summary>
    private static string TrimText(string content, int limit)
    {
        var text = Regex.Replace(content, @"\s+", " ").Trim();

        if (text.Length <= limit)
            return text;

        var window = text[..(limit + 20)];
        var sentenceEnd = window.LastIndexOfAny(['.', '!', '?']);

        if (sentenceEnd > limit / 3)
            return window[..(sentenceEnd + 1)];

        var wordEnd = window.LastIndexOf(' ');
        var shortened = wordEnd > limit / 3 ? window[..wordEnd] : window;

        return shortened.TrimEnd(',', ';', ':', ' ') + "…";
    }

    /// <summary>Builds a search query, dropping projection suffixes such as "3D" or "(2D)".</summary>
    [GeneratedRegex(@"[\s\-]*\(?[234]d\)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectionSuffixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static string BuildSearchQuery(string title) =>
        ProjectionSuffixRegex().Replace(title.Trim(), string.Empty).Trim();

    private static string NormalizeTitle(string? title) =>
        WhitespaceRegex().Replace((title ?? string.Empty).ToLowerInvariant(), " ").Trim();

    private sealed record TmdbSearch(List<TmdbSearchMovie>? Results);
    private sealed record TmdbSearchMovie(int Id, string? Title, string? OriginalTitle, string? Overview, double VoteAverage, double Popularity);
    private sealed record TmdbReviewList(List<TmdbReview>? Results);
    private sealed record TmdbReview(string Author, string Content);
    private sealed record TmdbExternalIds(string? ImdbId);
}
