namespace Cine.Models;

public class Movie
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public bool IsCartoon { get; set; }
    public List<ShowTime> Showtimes { get; set; } = [];
}

public class ShowTime
{
    public DateOnly Date { get; set; }
    public string Time { get; set; } = string.Empty;
}
