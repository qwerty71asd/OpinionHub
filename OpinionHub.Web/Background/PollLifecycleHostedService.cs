using OpinionHub.Web.Services;

namespace OpinionHub.Web.Background;

public class PollLifecycleHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PollLifecycleHostedService> _logger;

    public PollLifecycleHostedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<PollLifecycleHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPollService>();
                var completed = await service.CompleteExpiredPollsAsync();
                var archived = await service.ArchiveOldPollsAsync(_configuration.GetValue<int>("ArchiveAfterDays", 30));
                if (completed > 0 || archived > 0)
                    _logger.LogInformation("Lifecycle tick done. Completed={Completed}, Archived={Archived}", completed, archived);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lifecycle job failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
