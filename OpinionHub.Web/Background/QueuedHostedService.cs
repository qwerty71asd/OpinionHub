namespace OpinionHub.Web.Background;

/// <summary>
/// Consumer для <see cref="IBackgroundTaskQueue"/>. Каждое workItem выполняется в отдельном
/// DI-scope — это позволяет резолвить Scoped-сервисы (DbContext, *Service), не таща их из
/// контекста запроса (тот уже завершён).
/// </summary>
public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(
        IBackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<QueuedHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueuedHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task>? workItem = null;
            try
            {
                workItem = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (workItem is null) continue;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                // Per-item swallow — одна упавшая задача не должна валить весь consumer.
                _logger.LogError(ex, "Background work item failed.");
            }
        }

        _logger.LogInformation("QueuedHostedService stopped.");
    }
}
