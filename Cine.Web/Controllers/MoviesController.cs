using Cine.Models;
using Cine.Service;
using Cine.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Cine.Web.Controllers;

public class MoviesController(ParsingMoviesService parsingMoviesService, TmdbMovieInfoService tmdbMovieInfoService, ILogger<MoviesController> logger) : Controller
{
    [HttpGet("/upcoming")]
    public async Task<IActionResult> Upcoming(CancellationToken cancellationToken)
    {
        try
        {
            var result = await parsingMoviesService.GetUpcomingMoviesAsync(cancellationToken);
            await tmdbMovieInfoService.EnrichMoviesAsync(result.Movies, cancellationToken);
            return View(new UpcomingMoviesViewModel
            {
                Movies = result.Movies
                    .OrderBy(movie => movie.ReleaseDate ?? DateOnly.MaxValue)
                    .ThenBy(movie => movie.Title)
                    .ToList(),
                IsDataStale = result.IsStale,
                DataFetchedAt = result.FetchedAt,
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogError(exception, "Could not load upcoming movies");
            return View(new UpcomingMoviesViewModel { ErrorMessage = "CineStar is currently unavailable. Please try again later." });
        }
    }

    [HttpGet("/wishlist")]
    public async Task<IActionResult> Wishlist(CancellationToken cancellationToken)
    {
        try
        {
            var result = await parsingMoviesService.GetMoviesAsync(cancellationToken);
            return View(new WishlistViewModel
            {
                Movies = result.Movies.OrderBy(movie => movie.Title).ToList(),
                IsDataStale = result.IsStale,
                DataFetchedAt = result.FetchedAt,
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogError(exception, "Could not load wishlist schedule");
            return View(new WishlistViewModel { ErrorMessage = "CineStar is currently unavailable. Please try again later." });
        }
    }

    public async Task<IActionResult> Index(DateOnly? date, string? search, string? sort, bool cartoons = false, CancellationToken cancellationToken = default)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var searchTerm = search?.Trim() ?? string.Empty;
        var movieViewModel = new MoviesViewModel
        {
            SelectedDate = selectedDate,
            SearchTerm = searchTerm,
            Sort = sort ?? "time",
            ShowCartoons = cartoons
        };

        try
        {
            var result = await parsingMoviesService.GetMoviesAsync(cancellationToken);
            var movies = result.Movies
                .Select(movie => new Movie
                {
                    Title = movie.Title,
                    Description = movie.Description,
                    Price = movie.Price,
                    DetailUrl = movie.DetailUrl,
                    IsCartoon = movie.IsCartoon,
                    ReleaseDate = movie.ReleaseDate,
                    Showtimes = movie.Showtimes.Where(showtime => showtime.Date == selectedDate).ToList()
                })
                .Where(movie => movie.Showtimes.Count > 0)
                .ToList();

            if (!cartoons)
                movies = movies.Where(movie => !movie.IsCartoon).ToList();
            if (!string.IsNullOrWhiteSpace(searchTerm))
                movies = movies.Where(movie => movie.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                                               || movie.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

            movieViewModel.Movies = (movieViewModel.Sort.Equals("title", StringComparison.OrdinalIgnoreCase)
                    ? movies.OrderBy(movie => movie.Title)
                    : movies.OrderBy(movie => movie.Showtimes.Min(showtime => showtime.Time)))
                .ToList();
            await tmdbMovieInfoService.EnrichMoviesAsync(movieViewModel.Movies, cancellationToken);
            movieViewModel.AvailableDates = result.Movies
                .SelectMany(movie => movie.Showtimes)
                .Select(showtime => showtime.Date)
                .Distinct()
                .OrderBy(dateValue => dateValue)
                .ToList();
            movieViewModel.IsDataStale = result.IsStale;
            movieViewModel.DataFetchedAt = result.FetchedAt;
            movieViewModel.ErrorMessage = result.ErrorMessage;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogError(exception, "Could not load movie schedule");
            movieViewModel.ErrorMessage = "CineStar is currently unavailable. Please try again later.";
        }

        return View(movieViewModel);
    }
}
