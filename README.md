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
- Keeps the selected date and schedule easy to scan on mobile
- Caches scraped data for one hour to avoid unnecessary requests

## Title Variants

CineStar does not always use exactly the same title on every day. For example, a film may appear as `Avengers Premier` on one date and `Avengers` on another.

The app therefore treats the displayed title as schedule data rather than as a permanent identifier. Search also matches partial titles, so searching for `Avengers` finds both variants. The detail link comes from the individual CineStar movie entry instead of being generated from the title.

## Project Structure

```text
Cine/
├── Cine/                       # Shared models and CineStar scraper
│   ├── Models/Movie.cs
│   └── Service/ParsingMoviesService.cs
├── Cine.Web/                   # ASP.NET Core MVC web app
│   ├── Controllers/MoviesController.cs
│   ├── Models/MoviesViewModel.cs
│   ├── Views/Movies/Index.cshtml
│   └── wwwroot/                # Page styling and scroll behavior
├── Cine.sln
└── README.md
```

## Requirements

- .NET SDK 10.0 or later
- Network access to `cinestarcinemas.ba`

## Run Locally

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

A background service warms the cache and refreshes it approximately once per hour while the web app is running.

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

If CineStar changes its markup, update the selectors in `Cine/Service/ParsingMoviesService.cs`.
