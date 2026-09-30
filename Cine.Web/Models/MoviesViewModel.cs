using Cine.Models;

namespace Cine.Web.Models;

public class MoviesViewModel
{
    public List<Movie> Movies { get; set; } = [];
    public DateOnly SelectedDate { get; set; }
    public List<DateOnly> AvailableDates { get; set; } = [];
    public string SearchTerm { get; set; } = string.Empty;
    public string Sort { get; set; } = "time";
    public bool ShowCartoons { get; set; }
    public bool IsDataStale { get; set; }
    public DateTimeOffset? DataFetchedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int MovieCount => Movies.Count;
}
