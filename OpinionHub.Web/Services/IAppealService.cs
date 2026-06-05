namespace OpinionHub.Web.Services;

public class AppealResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? AppealId { get; init; }

    public static AppealResult Ok(Guid id) => new() { Success = true, AppealId = id };
    public static AppealResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public interface IAppealService
{
    Task<AppealResult> CreateAsync(string userName, string password, string message);
    Task<bool> ApproveAsync(Guid appealId, string adminId, string? response);
    Task<bool> RejectAsync(Guid appealId, string adminId, string? response);
    Task<bool> HasPendingAsync(string userId);

    /// <summary>
    /// История апелляций по юзеру — для отображения "X подавал, Y одобрено" в админке.
    /// `excludeAppealId` (если задан) исключает указанную запись (например, ту, что прямо
    /// сейчас рендерится в карточке — не нужно её дублировать в истории).
    /// </summary>
    Task<IReadOnlyList<Models.Appeal>> GetUserHistoryAsync(string userId, Guid? excludeAppealId = null);
}
