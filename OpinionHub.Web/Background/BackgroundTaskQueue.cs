using System.Threading.Channels;

namespace OpinionHub.Web.Background;

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    // Bounded — чтобы при недоступном Telegram очередь не съела всю память. FullMode.Wait
    // означает, что Producer (контроллер/сервис) будет асинхронно ждать слот; для учебного
    // проекта это нормальный backpressure-сценарий и проще DropOldest.
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel;

    public BackgroundTaskQueue()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        };
        _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
    }

    public ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        if (workItem is null) throw new ArgumentNullException(nameof(workItem));
        return _channel.Writer.WriteAsync(workItem);
    }

    public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
