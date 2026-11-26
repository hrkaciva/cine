namespace Cine.Models;

public class MovieInfo
{
    public double? Score { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string ImdbUrl { get; set; } = string.Empty;
    public string LetterboxdUrl { get; set; } = string.Empty;
    public List<MovieReview> Reviews { get; set; } = [];
}

public class MovieReview
{
    public string Author { get; set; } = string.Empty;

    /// <summary>Short quote shown on the movie card.</summary>
    public string Quote { get; set; } = string.Empty;

    /// <summary>Fullest version of the review, shown in the More info dialog.</summary>
    public string Content { get; set; } = string.Empty;
}
