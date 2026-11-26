# CineStar Schedule

A small local web app that turns the CineStar Mostar schedule into a clear, practical overview.

The CineStar page contains useful information, but it can be difficult to scan quickly when comparing dates, films, and showtimes. This project fetches that schedule and presents it as a focused daily cinema guide.

## What It Does

- Shows available screening dates in a compact date strip
- Filters movies by title or description
- Sorts movies by earliest showtime or title
- Hides cartoons by default, with an option to include them
- Highlights weekday screenings starting around 16:00 and 17:00
- Shows each movie's description and ticket price when available
- Links each movie to its CineStar detail page
- Saves movies to a browser-local wishlist and shows their current screening times; saved films that leave the schedule stay listed (marked as not currently showing) until removed
- Provides an `/upcoming` page for movies announced on CineStar's upcoming-films page, ordered by release date
- Shows each upcoming movie's announced release date when available
- Enriches schedule and upcoming movies with TMDB details: a score badge, short review quotes and IMDb/Letterboxd links on each card (cards keep CineStar's own description), plus a "More info" dialog with the fuller English description and complete review excerpts
- Keeps the selected date and schedule easy to scan on mobile
- Caches scraped data for one hour to avoid unnecessary requests
- Falls back to the last successful scrape when CineStar is temporarily unavailable

## Title Variants

CineStar does not always use exactly the same title on every day. For example, a film may appear as `Avengers Premier` on one date and `Avengers` on another.

The app therefore treats the displayed title as schedule data rather than as a permanent identifier. Search also matches partial titles, so searching for `Avengers` finds both variants. The detail link comes from the individual CineStar movie entry instead of being generated from the title.

## Project Structure

```text
Cine/
├── Cine/                       # Shared models, CineStar scraper, TMDB enrichment
│   ├── Models/Movie.cs
│   ├── Models/MovieInfo.cs
│   ├── Service/ParsingMoviesService.cs
│   └── Service/TmdbMovieInfoService.cs
├── Cine.Web/                   # ASP.NET Core MVC web app
│   ├── Controllers/MoviesController.cs
│   ├── Models/MoviesViewModel.cs and WishlistViewModel.cs
│   ├── Views/Movies/Index.cshtml
│   ├── Views/Movies/Wishlist.cshtml
│   ├── Views/Movies/Upcoming.cshtml
│   └── wwwroot/                # Page styling and scroll behavior
├── Cine.sln
└── README.md
```

## Requirements

- .NET SDK 10.0 or later
- Network access to `cinestarcinemas.ba`
- A free [TMDB](https://www.themoviedb.org/) API key for movie enrichment (optional; without it the app still works and simply shows CineStar's own descriptions)

## Run Locally

Store your TMDB API key in user-secrets so it is never committed (once per machine, from the repository root):

```bash
dotnet user-secrets init --project Cine.Web
dotnet user-secrets set "Tmdb:ApiKey" "your_key" --project Cine.Web
```

From the repository root:

```bash
dotnet restore Cine.sln
dotnet run --project Cine.Web/Cine.Web.csproj
```

Open the local URL printed by ASP.NET Core, usually one of:

```text
https://localhost:xxxx
http://localhost:xxxx
```

The default route opens the movie schedule directly.

Open `/upcoming` to see movies scraped from CineStar's upcoming-films page. The page is linked from the main schedule but is not part of the site's primary header navigation.

## Useful Query Parameters

The filters are represented in the URL, so a view can be bookmarked or shared.

```text
/Movies?date=2026-09-17&search=avengers&sort=time&cartoons=false
```

- `date`: selected screening date in `yyyy-MM-dd` format
- `search`: partial movie title or description search
- `sort`: `time` or `title`
- `cartoons`: `true` to include cartoons; omitted or `false` hides them

## Data Flow

1. `ParsingMoviesService` downloads the CineStar Mostar page.
2. Html Agility Pack extracts movie titles, descriptions, prices, detail links, dates, and showtimes.
3. Results are cached in memory for one hour.
4. `MoviesController` applies the selected date, search, cartoon filter, and sort order.
5. The MVC view renders the schedule in a responsive layout.

The `/upcoming` action discovers the current “Uskoro” link from the CineStar homepage, then uses a separate one-hour cache for the resulting movie list. This avoids depending on CineStar's numeric page suffix. The list is ordered by release date, with undated titles last.

A background service warms the cache and refreshes both the schedule and upcoming-movies data approximately once per hour while the web app is running. Requests use a 15-second timeout and identify the app with a User-Agent header. If a refresh fails, the last successful data is retained and the page marks it as stale.

## TMDB Enrichment

`TmdbMovieInfoService` matches each visible movie title against TMDB and attaches a score, the English overview, up to three review quotes, and IMDb/Letterboxd links (the Letterboxd link uses their IMDb-based alias route, so no scraping is involved). The movie card keeps CineStar's own description and shows up to two short quotes; the "More info" dialog shows the fuller TMDB description and the review excerpts at greater length.

- Lookups are cached per title for 24 hours; titles without a match are remembered for 6 hours so unknown localized titles are not re-searched on every request.
- Only movies shown after filtering are enriched, in parallel, which keeps the request volume small.
- Titles are matched by exact title (including the original title) first, then by the most popular result; projection suffixes such as `3D` are stripped before searching.
- Network failures are never cached; they just leave that movie with the regular CineStar description, and the lookup is retried on the next request.
- Without a configured API key the enrichment is silently skipped.

If TMDB's response shape changes, update the record types at the bottom of `Cine/Service/TmdbMovieInfoService.cs`.

## Development Checks

Build the web project with:

```bash
dotnet build Cine.Web/Cine.Web.csproj
```

Generated `bin/` and `obj/` directories, IDE settings, and user-specific files are excluded through `.gitignore`.

## Scraping Notes

The source page is external and its HTML can change. Optional fields are handled defensively:

- Missing descriptions fall back to a short placeholder.
- Missing prices show `Tickets at box office`.
- Missing movie links fall back to the CineStar Mostar page.
- Cartoon detection uses the source class and common animation labels.
- Release dates are parsed from the source's `najava` notice (`Početak prikazivanja: dd.MM.yyyy.`). Movies without an announced date keep a generic Coming soon badge.
- Empty or changed result markup is treated as a failed refresh instead of replacing valid cached data.
- Date parsing considers the previous, current, and next year so schedules around New Year remain usable.

If CineStar changes its markup, update the selectors in `Cine/Service/ParsingMoviesService.cs`.
