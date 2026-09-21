using Cine.Models;
using Cine.Service;
using Cine.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Cine.Web.Controllers;

public class MoviesController(ParsingMoviesService parsingMoviesService) : Controller
{
    [HttpGet("/upcoming")]
    public async Task<IActionResult> Upcoming()
    {
        var movies = (await parsingMoviesService.GetUpcomingMoviesAsync())
            .OrderBy(movie => movie.Title)
            .ToList();

        return View(movies);
    }

    public async Task<IActionResult> Index(DateOnly? date, string? search, string? sort, bool cartoons = false)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Now);

        var movies = await parsingMoviesService.GetMovieByDateAsync(selectedDate);
        var searchTerm = search?.Trim() ?? string.Empty;
        if (!cartoons)
            movies = movies.Where(movie => !movie.IsCartoon).ToList();
        if (!string.IsNullOrWhiteSpace(searchTerm))
            movies = movies.Where(movie => movie.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                                           || movie.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        movies = (sort ?? "time").Equals("title", StringComparison.OrdinalIgnoreCase)
            ? movies.OrderBy(movie => movie.Title).ToList()
            : movies.OrderBy(movie => movie.Showtimes.Select(showtime => showtime.Time).FirstOrDefault()).ToList();

        var movieViewModel = new MoviesViewModel
        {
            Movies = movies,
            SelectedDate = selectedDate,
            AvailableDates = await parsingMoviesService.GetMovieDatesAsync(),
            SearchTerm = searchTerm,
            Sort = sort ?? "time",
            ShowCartoons = cartoons
        };

        return View(movieViewModel);
    }
}
