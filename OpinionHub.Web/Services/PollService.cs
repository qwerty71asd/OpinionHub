using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public class PollService : IPollService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PollService> _logger;
    private readonly IFileStorageService _fileStorage; // Добавляем сервис хранилища

    public PollService(ApplicationDbContext db, ILogger<PollService> logger, IFileStorageService fileStorage)
    {
        _db = db;
        _logger = logger;
        _fileStorage = fileStorage;
    }

    public async Task<Poll> CreateDraftAsync(CreatePollViewModel model, string authorId)
    {
        var title = model.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Название опроса не может быть пустым.");

        DateTime? endUtc = null;
        if (model.EndDateUtc.HasValue)
        {
            var raw = model.EndDateUtc.Value;
            var asLocal = raw.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(raw, DateTimeKind.Local)
                : raw.ToLocalTime();
            endUtc = asLocal.ToUniversalTime();

            if (endUtc.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Дата окончания должна быть в будущем.");
        }

        // 1. Обработка заглавного фото
        string? coverPath = null;
        if (model.CoverImage != null)
        {
            coverPath = await _fileStorage.SaveFileAsync(model.CoverImage, "covers");
        }

        // 2. Создание объекта опроса
        var poll = new Poll
        {
            Title = title,
            PollType = model.PollType,
            VisibilityType = model.VisibilityType,
            AudienceType = model.AudienceType,
            CanChangeVote = model.CanChangeVote,
            EndDateUtc = endUtc,
            AuthorId = authorId,
            Status = model.PublishNow ? PollStatus.Active : PollStatus.Draft,
            CoverImagePath = coverPath, // Сохраняем путь к обложке
            AllowedUsers = model.AudienceType == AudienceType.SelectedUsers
                ? (model.AllowedUserIds ?? new List<string>()).Select(uid => new PollAllowedUser { UserId = uid }).ToList()
                : new List<PollAllowedUser>()
        };

        // 3. Обработка вариантов ответа с картинками
        foreach (var optVm in model.Options ?? new List<CreatePollOptionVm>())
        {
            if (string.IsNullOrWhiteSpace(optVm.Text)) continue;

            string? optImagePath = null;
            if (optVm.Image != null)
            {
                optImagePath = await _fileStorage.SaveFileAsync(optVm.Image, "options");
            }

            poll.Options.Add(new PollOption
            {
                Text = optVm.Text.Trim(),
                ImagePath = optImagePath
            });
        }

        if (poll.Options.Count < 2)
            throw new InvalidOperationException("Нужно минимум 2 уникальных варианта.");

        // 4. Обработка дополнительных вложений
        if (model.AttachedFiles != null && model.AttachedFiles.Count > 0)
        {
            foreach (var file in model.AttachedFiles)
            {
                var filePath = await _fileStorage.SaveFileAsync(file, "attachments");
                poll.Attachments.Add(new PollAttachment
                {
                    FilePath = filePath,
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length
                });
            }
        }

        _db.Polls.Add(poll);
        _db.AuditLogs.Add(new AuditLog { EventType = "POLL_CREATED", PollId = poll.Id, UserId = authorId, Details = poll.Title });

        await _db.SaveChangesAsync();
        return poll;
    }

    public async Task PublishAsync(Guid pollId, string authorId)
    {
        var poll = await _db.Polls.FirstOrDefaultAsync(p => p.Id == pollId && p.AuthorId == authorId);
        if (poll is null) throw new InvalidOperationException("Опрос не найден.");
        if (poll.Status != PollStatus.Draft) throw new InvalidOperationException("Публикуются только черновики.");
        poll.Status = PollStatus.Active;
        await _db.SaveChangesAsync();
    }

    public async Task VoteAsync(Guid pollId, string userId, IReadOnlyCollection<Guid> optionIds)
    {
        var poll = await _db.Polls
            .Include(p => p.Options)
            .Include(p => p.AllowedUsers)
            .FirstOrDefaultAsync(p => p.Id == pollId);
        if (poll is null) throw new InvalidOperationException("Опрос не найден");

        if (!IsAllowed(poll, userId))
            throw new UnauthorizedAccessException("У вас нет доступа к этому опросу.");
        if (poll.Status != PollStatus.Active) throw new InvalidOperationException("Голосование недоступно");
        if (poll.EndDateUtc.HasValue && poll.EndDateUtc.Value <= DateTime.UtcNow) throw new InvalidOperationException("Срок истек");
        if (poll.PollType == PollType.SingleChoice && optionIds.Count != 1) throw new InvalidOperationException("Нужно выбрать 1 вариант");
        if (optionIds.Count == 0) throw new InvalidOperationException("Выберите хотя бы один вариант");

        var validOptionIds = poll.Options.Select(o => o.Id).ToHashSet();
        if (optionIds.Any(o => !validOptionIds.Contains(o))) throw new InvalidOperationException("Некорректный вариант ответа");

        // Важный участок: мы не создаем новый голос при пере-голосовании, чтобы сохранить гарантию
        // "один голос на аккаунт", а обновляем существующую запись и фиксируем это в аудит-логе.
        var existing = await _db.Votes.Include(v => v.Selections)
            .FirstOrDefaultAsync(v => v.PollId == pollId && v.VoterAccountId == userId);

        if (existing is not null && !poll.CanChangeVote)
            throw new InvalidOperationException("Изменение голоса запрещено автором");

        if (existing is null)
        {
            existing = new Vote
            {
                PollId = pollId,
                // В отличие от UserId (который может быть null для анонимного режима),
                // этот идентификатор всегда сохраняем для правила "1 аккаунт = 1 голос".
                VoterAccountId = userId,
                UserId = poll.VisibilityType == VisibilityType.Anonymous ? null : userId
            };
            _db.Votes.Add(existing);
        }
        else
        {
            _db.VoteSelections.RemoveRange(existing.Selections);
        }

        existing.Selections = optionIds.Select(id => new VoteSelection { VoteId = existing.Id, PollOptionId = id }).ToList();

        _db.AuditLogs.Add(new AuditLog
        {
            EventType = "VOTE_SUBMITTED",
            PollId = pollId,
            UserId = poll.VisibilityType == VisibilityType.Anonymous ? null : userId,
            Details = $"Options={string.Join(',', optionIds)}"
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("Vote saved for poll {PollId} by {UserId}", pollId, userId);
    }

    public async Task<Poll?> GetPollDetailsAsync(Guid pollId, string? viewerUserId)
    {
        var poll = await _db.Polls
            .Include(p => p.Author)
            .Include(p => p.Options)
            .Include(p => p.AllowedUsers)
            .Include(p => p.Attachments)
            .Include(p => p.Votes).ThenInclude(v => v.Selections)
            .FirstOrDefaultAsync(p => p.Id == pollId);

        if (poll is null)
            return null;

        // НОВОЕ ПРАВИЛО: Если это черновик, смотреть его может ТОЛЬКО автор
        if (poll.Status == PollStatus.Draft && poll.AuthorId != viewerUserId)
            return null; // Вернем null, контроллер выдаст ошибку доступа

        // Публичный опрос доступен всем.
        if (poll.AudienceType == AudienceType.Everyone)
            return poll;

        // Закрытый: доступен только автору и выбранным пользователям.
        if (viewerUserId is null)
            return null;

        return IsAllowed(poll, viewerUserId) ? poll : null;
    }

    public async Task<IReadOnlyCollection<Poll>> GetFeedAsync(string? viewerUserId)
    {
        var q = _db.Polls
            .Include(p => p.Options)
            .AsQueryable();

        if (string.IsNullOrWhiteSpace(viewerUserId))
        {
            // Гости видят только публичные опросы, которые УЖЕ опубликованы (не черновики)
            q = q.Where(p => p.AudienceType == AudienceType.Everyone && p.Status != PollStatus.Draft);
        }
        else
        {
            var uid = viewerUserId;
            // Авторизованные видят:
            // 1. ВСЕ свои опросы (свои черновики видеть нужно)
            // 2. Чужие опросы, ТОЛЬКО если они НЕ черновики И (публичные ИЛИ юзер есть в списке допущенных)
            q = q.Where(p =>
                p.AuthorId == uid
                || (p.Status != PollStatus.Draft && (p.AudienceType == AudienceType.Everyone || p.AllowedUsers.Any(a => a.UserId == uid)))
            );
        }

        return await q
            .OrderBy(p => p.Status == PollStatus.Active ? 0 : 1)
            .ThenByDescending(p => p.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<byte[]> ExportCsvAsync(Guid pollId, string userId)
    {
        var poll = await EnsureExportAccess(pollId, userId);
        var sb = new StringBuilder();
        sb.AppendLine("Option,Votes,Percent");
        var total = poll.Votes.Count;
        foreach (var option in poll.Options)
        {
            var count = poll.Votes.Count(v => v.Selections.Any(s => s.PollOptionId == option.Id));
            var pct = total == 0 ? 0 : count * 100.0 / total;
            sb.AppendLine($"\"{option.Text}\",{count},{pct:F2}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportXlsxAsync(Guid pollId, string userId)
    {
        var poll = await EnsureExportAccess(pollId, userId);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Results");
        ws.Cell(1, 1).Value = "Option";
        ws.Cell(1, 2).Value = "Votes";
        ws.Cell(1, 3).Value = "Percent";
        var total = poll.Votes.Count;

        for (var i = 0; i < poll.Options.Count; i++)
        {
            var option = poll.Options.ElementAt(i);
            var count = poll.Votes.Count(v => v.Selections.Any(s => s.PollOptionId == option.Id));
            var pct = total == 0 ? 0 : count * 100.0 / total;
            ws.Cell(i + 2, 1).Value = option.Text;
            ws.Cell(i + 2, 2).Value = count;
            ws.Cell(i + 2, 3).Value = pct;
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<int> CompleteExpiredPollsAsync()
    {
        var now = DateTime.UtcNow;
        var polls = await _db.Polls.Where(p => p.Status == PollStatus.Active && p.EndDateUtc != null && p.EndDateUtc <= now).ToListAsync();
        foreach (var poll in polls)
        {
            poll.Status = PollStatus.Completed;
            poll.CompletedAtUtc = now;
            _db.AuditLogs.Add(new AuditLog { EventType = "POLL_COMPLETED", PollId = poll.Id, Details = "Auto complete" });
        }
        await _db.SaveChangesAsync();
        return polls.Count;
    }

    public async Task<int> ArchiveOldPollsAsync(int archiveAfterDays)
    {
        var threshold = DateTime.UtcNow.AddDays(-archiveAfterDays);
        var polls = await _db.Polls.Where(p => p.Status == PollStatus.Completed && p.CompletedAtUtc < threshold).ToListAsync();
        foreach (var poll in polls)
        {
            poll.Status = PollStatus.Archived;
            _db.AuditLogs.Add(new AuditLog { EventType = "POLL_ARCHIVED", PollId = poll.Id, Details = "Auto archive" });
        }
        await _db.SaveChangesAsync();
        return polls.Count;
    }

    private async Task<Poll> EnsureExportAccess(Guid pollId, string userId)
    {
        var poll = await _db.Polls
            .Include(p => p.Options)
            .Include(p => p.AllowedUsers)
            .Include(p => p.Votes).ThenInclude(v => v.Selections)
            .FirstOrDefaultAsync(p => p.Id == pollId);
        if (poll is null) throw new InvalidOperationException("Опрос не найден");
        if (poll.AuthorId != userId) throw new UnauthorizedAccessException("Экспорт доступен только автору");
        return poll;
    }

    private static bool IsAllowed(Poll poll, string userId)
    {
        if (poll.AudienceType == AudienceType.Everyone)
            return true;

        if (poll.AuthorId == userId)
            return true;

        return poll.AllowedUsers.Any(x => x.UserId == userId);
    }
    public async Task DeleteAsync(Guid pollId, string userId)
    {
        var poll = await _db.Polls.FirstOrDefaultAsync(p => p.Id == pollId);

        if (poll is null)
            throw new InvalidOperationException("Опрос не найден.");

        if (poll.AuthorId != userId)
            throw new UnauthorizedAccessException("Удалять опросы может только создатель.");

        _db.Polls.Remove(poll);

        // Оставляем след в истории, что опрос был удален
        _db.AuditLogs.Add(new AuditLog
        {
            EventType = "POLL_DELETED",
            PollId = pollId,
            UserId = userId,
            Details = $"Удален опрос: {poll.Title}"
        });

        await _db.SaveChangesAsync();
    }
    public async Task<List<Poll>> GetUserPollsAsync(string userId)
    {
        return await _db.Polls
            .Where(p => p.AuthorId == userId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<List<Poll>> GetVotedPollsAsync(string userId)
    {
        return await _db.Polls
            .Include(p => p.Author)
            .Where(p => p.Votes.Any(v => v.VoterAccountId == userId))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();
    }

}
