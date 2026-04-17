using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public interface IPollService
{
    Task<Poll> CreateDraftAsync(CreatePollViewModel model, string authorId);
    Task PublishAsync(Guid pollId, string authorId);
    Task VoteAsync(Guid pollId, string userId, IReadOnlyCollection<Guid> optionIds);
    Task<Poll?> GetPollDetailsAsync(Guid pollId, string? viewerUserId);
    Task<IReadOnlyCollection<Poll>> GetFeedAsync(string? viewerUserId);
    Task<byte[]> ExportCsvAsync(Guid pollId, string userId);
    Task<byte[]> ExportXlsxAsync(Guid pollId, string userId);
    Task<int> CompleteExpiredPollsAsync();
    Task<int> ArchiveOldPollsAsync(int archiveAfterDays);
    Task DeleteAsync(Guid pollId, string userId);
    Task<List<Poll>> GetUserPollsAsync(string userId);
}
