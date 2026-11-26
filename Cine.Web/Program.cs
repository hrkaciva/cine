using Cine.Service;
using Cine.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient<ParsingMoviesService>(client =>
{
    client.BaseAddress = new Uri("https://cinestarcinemas.ba/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CineStarSchedule/1.0");
});

builder.Services.AddScoped<ParsingMoviesService>();

// TMDB enrichment client. The API key is read from configuration (user-secrets in
// development) and passed into the service, keeping the Cine library config-free.
builder.Services.AddHttpClient(TmdbMovieInfoService.ClientName, client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CineStarSchedule/1.0");
});
builder.Services.AddScoped<TmdbMovieInfoService>(serviceProvider => new TmdbMovieInfoService(
    serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(TmdbMovieInfoService.ClientName),
    serviceProvider.GetRequiredService<IMemoryCache>(),
    builder.Configuration["Tmdb:ApiKey"] ?? string.Empty,
    serviceProvider.GetRequiredService<ILogger<TmdbMovieInfoService>>()));

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<ScraperBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Movies}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
