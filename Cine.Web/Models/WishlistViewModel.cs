using Cine.Models;

namespace Cine.Web.Models;

public class WishlistViewModel
{
    public List<Movie> Movies { get; set; } = [];
    public bool IsDataStale { get; set; }
    public DateTimeOffset? DataFetchedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
