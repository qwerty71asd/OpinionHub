namespace OpinionHub.Web.Background;

/// <summary>
/// Очередь fire-and-forget задач, которые нужно выполнить вне HTTP-реквеста.
/// Используется для Telegram-уведомлений, чтобы их сетевые таймауты не блокировали ответ юзеру.
///
/// Контракт workItem: ему передаётся свежий <see cref="IServiceProvider"/> из нового scope,
/// поэтому Scoped-сервисы (например, DbContext) безопасно резолвить ИЗ него — но НЕ захватывать
/// в замыкании из текущего scope, иначе после ответа пользователю поймаешь ObjectDisposedException.
/// </summary>
public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);

    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
