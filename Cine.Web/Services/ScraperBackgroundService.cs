using Cine.Service;

namespace Cine.Web.Services;

public class ScraperBackgroundService(ILogger<ScraperBackgroundService> logger, IServiceProvider serviceProvider)
    : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ScraperBackgroundService> _logger = logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraper background service started");


        while (!stoppingToken.IsCancellationRequested)
        {
            await ScrapeAsync();
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ScrapeAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ParsingMoviesService>();
            await service.GetMoviesAsync();
            _logger.LogInformation("Scraper completed at {Time}", DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scraper failed at {Time}", DateTime.Now);
        }
    }
}