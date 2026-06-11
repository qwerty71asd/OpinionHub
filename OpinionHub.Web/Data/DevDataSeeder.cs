using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Data;

/// <summary>
/// Заполняет БД детерминированным набором тестовых данных для Development-среды:
/// 10 обычных юзеров (user01..user10), 15 опросов с разными статусами/типами/видимостью,
/// голоса, комментарии (с реплаями), лайки опросов и комментариев, подписки,
/// жалобы и апелляции. user10 — забанен, на него заведена Pending-апелляция.
///
/// Активируется флагом SeedDevData:Enabled (или автоматически в Development).
/// Идемпотентен: если user01 уже существует, сидинг пропускается целиком.
/// Опросы создаются напрямую через DbContext (не через PollService) — чтобы не
/// триггерить Telegram-рассылки подписчикам.
/// </summary>
public static class DevDataSeeder
{
    private const string CommonPassword = "Test12345";
    private const int RngSeed = 42;

    public static async Task SeedAsync(
        IServiceProvider services,
        ILogger logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var enabled = configuration.GetValue<bool?>("SeedDevData:Enabled") ?? environment.IsDevelopment();
        if (!enabled)
            return;

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Idempotency: если есть хотя бы один сидированный user — выходим.
            if (await userManager.FindByNameAsync("user01") is not null)
            {
                logger.LogInformation("DevDataSeeder: user01 уже существует, сидинг пропущен.");
                return;
            }

            var admin = await userManager.FindByNameAsync("admin");
            if (admin is null)
            {
                logger.LogWarning("DevDataSeeder: admin не найден — сидинг прерван. Сначала должен отработать DbInitializer.SeedAdmin.");
                return;
            }

            var rng = new Random(RngSeed);

            var users = await SeedUsersAsync(userManager, admin.Id, rng, logger);
            if (users.Count < 10)
            {
                logger.LogWarning("DevDataSeeder: создано меньше 10 юзеров ({Count}) — дальнейшие шаги пропущены.", users.Count);
                return;
            }

            var pollAuthors = users.Take(9).ToList(); // user01..user09 — авторы; user10 забанен.
            var (polls, optionsByPoll) = await SeedPollsAsync(db, pollAuthors, users, rng);
            var voteCount = await SeedVotesAsync(db, polls, optionsByPoll, users, rng);
            var (rootCount, replyCount) = await SeedCommentsAsync(db, polls, users, rng);
            await SeedPollLikesAsync(db, polls, users, rng);
            await SeedCommentLikesAsync(db, users, rng);
            await SeedSubscriptionsAsync(db, users, admin, rng);
            await SeedReportsAsync(db, polls, users, admin.Id, rng);
            await SeedAppealsAsync(db, users[9], admin.Id);

            logger.LogInformation(
                "DevDataSeeder: создано 10 пользователей (1 забанен), {Polls} опросов, {Votes} голосов, {Root} root-комментариев + {Replies} reply, лайки/подписки/жалобы/апелляции.",
                polls.Count, voteCount, rootCount, replyCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DevDataSeeder: ошибка при сидинге тестовых данных. Запуск приложения продолжится, но БД может оказаться в частично заполненном состоянии.");
        }
    }

    // ----------------------- USERS -----------------------

    private static async Task<List<ApplicationUser>> SeedUsersAsync(
        UserManager<ApplicationUser> userManager,
        string adminId,
        Random rng,
        ILogger logger)
    {
        var result = new List<ApplicationUser>();

        for (int i = 1; i <= 10; i++)
        {
            var userName = $"user{i:D2}";
            var email = $"{userName}@local";

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                TelegramChatId = null,

                // Разные настройки приватности у разных юзеров.
                ShowLikedPolls = (i % 2 == 0),                 // user02, user04, user06, user08, user10
                ShowVotedPolls = (i % 3 != 0),                 // НЕ user03, user06, user09 — у остальных видно
                NotifyAnonymousPolls = (i % 4 == 0),           // user04, user08
                NotifyOnNewSubscriber = (i % 5 == 0),          // user05, user10
                NotifyOnCommentOnMyPoll = (i % 2 == 1),        // нечётные
                NotifyOnReplyToMyComment = (i % 3 == 0),       // user03, user06, user09
                NotifyOnLikeMilestone = (i % 7 == 0),          // user07

                Bio = (i % 3 == 0)
                    ? $"Тестовый пользователь #{i} — заглушка для проверки UI."
                    : null,
            };

            var create = await userManager.CreateAsync(user, CommonPassword);
            if (!create.Succeeded)
            {
                var errors = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
                logger.LogWarning("DevDataSeeder: не удалось создать {UserName}: {Errors}", userName, errors);
                continue;
            }

            var addRole = await userManager.AddToRoleAsync(user, Roles.User);
            if (!addRole.Succeeded)
            {
                logger.LogWarning("DevDataSeeder: не выдана роль User для {UserName}", userName);
            }

            // user10 — забанен; нужен для проверки апелляции.
            if (i == 10)
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

                user.BanReason = "Тестовый бан для проверки апелляции";
                user.BannedAt = DateTimeOffset.UtcNow.AddDays(-3);
                user.BannedById = adminId;
                await userManager.UpdateAsync(user);
            }

            result.Add(user);
        }

        return result;
    }

    // ----------------------- POLLS -----------------------

    private static async Task<(List<Poll> polls, Dictionary<Guid, List<PollOption>> options)> SeedPollsAsync(
        ApplicationDbContext db,
        IReadOnlyList<ApplicationUser> authors,
        IReadOnlyList<ApplicationUser> allUsers,
        Random rng)
    {
        var fixtures = PollFixtures();
        var polls = new List<Poll>(fixtures.Count);
        var optionsByPoll = new Dictionary<Guid, List<PollOption>>();

        // Распределение статусов: 2 Draft, 2 Completed, 1 Archived, 10 Active.
        var statuses = new PollStatus[15];
        statuses[0] = PollStatus.Draft;
        statuses[1] = PollStatus.Draft;
        statuses[2] = PollStatus.Completed;
        statuses[3] = PollStatus.Completed;
        statuses[4] = PollStatus.Archived;
        for (int i = 5; i < 15; i++) statuses[i] = PollStatus.Active;

        // Распределение видимости: 8 Public, 4 Anonymous, 3 Private.
        var visibilities = new VisibilityType[15];
        for (int i = 0; i < 8; i++) visibilities[i] = VisibilityType.Public;
        for (int i = 8; i < 12; i++) visibilities[i] = VisibilityType.Anonymous;
        for (int i = 12; i < 15; i++) visibilities[i] = VisibilityType.Private;

        // Перемешаем индексы, чтобы статусы и видимости не выстраивались по порядку.
        var orderByStatus = Enumerable.Range(0, 15).OrderBy(_ => rng.Next()).ToArray();
        var orderByVisibility = Enumerable.Range(0, 15).OrderBy(_ => rng.Next()).ToArray();

        // Какие индексы будут SelectedUsers (2 шт.).
        var selectedAudienceIdx = new HashSet<int>(Enumerable.Range(0, 15).OrderBy(_ => rng.Next()).Take(2));

        for (int i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            var status = statuses[orderByStatus[i]];
            var visibility = visibilities[orderByVisibility[i]];
            var pollType = rng.NextDouble() < 0.7 ? PollType.SingleChoice : PollType.MultipleChoice;
            var audience = selectedAudienceIdx.Contains(i) ? AudienceType.SelectedUsers : AudienceType.Everyone;
            var author = authors[rng.Next(authors.Count)];

            var createdAt = DateTime.UtcNow.AddDays(-rng.Next(1, 60));
            DateTime? endDate = status switch
            {
                PollStatus.Completed => createdAt.AddDays(rng.Next(1, 10)),
                PollStatus.Archived => createdAt.AddDays(rng.Next(10, 30)),
                PollStatus.Draft => null,
                _ => rng.NextDouble() < 0.7 ? DateTime.UtcNow.AddDays(rng.Next(3, 30)) : (DateTime?)null,
            };
            DateTime? completedAt = status == PollStatus.Completed ? endDate : null;

            bool isAnonAuthor = visibility == VisibilityType.Anonymous || (visibility == VisibilityType.Public && rng.NextDouble() < 0.15);

            var poll = new Poll
            {
                Title = fixture.Title,
                Description = fixture.Description,
                PollType = pollType,
                VisibilityType = visibility,
                AudienceType = audience,
                CanChangeVote = rng.NextDouble() < 0.5,
                EndDateUtc = endDate,
                Status = status,
                CreatedAtUtc = createdAt,
                CompletedAtUtc = completedAt,
                AuthorId = author.Id,
                IsAnonymousAuthor = isAnonAuthor,
                IsDeleted = false,
            };

            var pollOptions = new List<PollOption>();
            foreach (var text in fixture.Options)
            {
                var option = new PollOption { Text = text, PollId = poll.Id };
                poll.Options.Add(option);
                pollOptions.Add(option);
            }

            // Для SelectedUsers — добавим 3..5 разрешённых пользователей (не считая автора и user10).
            if (audience == AudienceType.SelectedUsers)
            {
                var pool = allUsers.Where(u => u.Id != author.Id && u.UserName != "user10").ToList();
                int allowedCount = Math.Min(rng.Next(3, 6), pool.Count);
                var allowed = pool.OrderBy(_ => rng.Next()).Take(allowedCount);
                foreach (var u in allowed)
                {
                    poll.AllowedUsers.Add(new PollAllowedUser { PollId = poll.Id, UserId = u.Id });
                }
            }

            polls.Add(poll);
            optionsByPoll[poll.Id] = pollOptions;
        }

        db.Polls.AddRange(polls);
        await db.SaveChangesAsync();

        return (polls, optionsByPoll);
    }

    // ----------------------- VOTES -----------------------

    private static async Task<int> SeedVotesAsync(
        ApplicationDbContext db,
        IReadOnlyList<Poll> polls,
        Dictionary<Guid, List<PollOption>> optionsByPoll,
        IReadOnlyList<ApplicationUser> users,
        Random rng)
    {
        var votes = new List<Vote>();

        foreach (var poll in polls)
        {
            if (poll.Status == PollStatus.Draft)
                continue;

            // Пул потенциальных голосующих: все, кроме автора.
            IEnumerable<ApplicationUser> eligible = users.Where(u => u.Id != poll.AuthorId);

            // Для SelectedUsers — только из allowed-списка.
            if (poll.AudienceType == AudienceType.SelectedUsers)
            {
                var allowedIds = poll.AllowedUsers.Select(a => a.UserId).ToHashSet();
                eligible = eligible.Where(u => allowedIds.Contains(u.Id));
            }

            var eligibleList = eligible.OrderBy(_ => rng.Next()).ToList();
            if (eligibleList.Count == 0)
                continue;

            int voterCount = Math.Min(rng.Next(2, 9), eligibleList.Count);
            var options = optionsByPoll[poll.Id];

            foreach (var voter in eligibleList.Take(voterCount))
            {
                var vote = new Vote
                {
                    PollId = poll.Id,
                    VoterAccountId = voter.Id,
                    UserId = poll.VisibilityType == VisibilityType.Anonymous ? null : voter.Id,
                    CreatedAtUtc = poll.CreatedAtUtc.AddDays(rng.Next(0, 5)).AddMinutes(rng.Next(0, 1440)),
                };

                if (poll.PollType == PollType.SingleChoice)
                {
                    var chosen = options[rng.Next(options.Count)];
                    vote.Selections.Add(new VoteSelection { VoteId = vote.Id, PollOptionId = chosen.Id });
                }
                else
                {
                    int selectCount = rng.Next(1, Math.Min(options.Count, 3) + 1);
                    var chosenIds = options.OrderBy(_ => rng.Next()).Take(selectCount).Select(o => o.Id);
                    foreach (var oid in chosenIds)
                    {
                        vote.Selections.Add(new VoteSelection { VoteId = vote.Id, PollOptionId = oid });
                    }
                }

                votes.Add(vote);
            }
        }

        db.Votes.AddRange(votes);
        await db.SaveChangesAsync();
        return votes.Count;
    }

    // ----------------------- COMMENTS -----------------------

    private static async Task<(int rootCount, int replyCount)> SeedCommentsAsync(
        ApplicationDbContext db,
        IReadOnlyList<Poll> polls,
        IReadOnlyList<ApplicationUser> users,
        Random rng)
    {
        var commentablePolls = polls.Where(p => p.Status != PollStatus.Draft).ToList();
        if (commentablePolls.Count == 0)
            return (0, 0);

        var rootTemplates = RootCommentTemplates();
        var replyTemplates = ReplyCommentTemplates();

        int total = rng.Next(30, 51);
        int rootTarget = (int)(total * 0.7);
        int replyTarget = total - rootTarget;

        // 1) ROOT
        var roots = new List<Comment>();
        for (int i = 0; i < rootTarget; i++)
        {
            var poll = commentablePolls[rng.Next(commentablePolls.Count)];
            var author = users[rng.Next(users.Count)];
            var createdAt = poll.CreatedAtUtc.AddDays(rng.Next(0, 7)).AddMinutes(rng.Next(0, 1440));

            roots.Add(new Comment
            {
                PollId = poll.Id,
                AuthorId = author.Id,
                Text = rootTemplates[rng.Next(rootTemplates.Length)],
                CreatedAtUtc = createdAt,
            });
        }
        db.Comments.AddRange(roots);
        await db.SaveChangesAsync();

        // 2) REPLY — выбираем parent из уже сохранённых roots.
        var replies = new List<Comment>();
        for (int i = 0; i < replyTarget; i++)
        {
            if (roots.Count == 0) break;
            var parent = roots[rng.Next(roots.Count)];
            var author = users[rng.Next(users.Count)];
            var createdAt = parent.CreatedAtUtc.AddHours(rng.Next(1, 72));

            replies.Add(new Comment
            {
                PollId = parent.PollId,
                AuthorId = author.Id,
                Text = replyTemplates[rng.Next(replyTemplates.Length)],
                CreatedAtUtc = createdAt,
                ParentCommentId = parent.Id,
            });
        }
        db.Comments.AddRange(replies);
        await db.SaveChangesAsync();

        return (roots.Count, replies.Count);
    }

    // ----------------------- POLL LIKES -----------------------

    private static async Task SeedPollLikesAsync(
        ApplicationDbContext db,
        IReadOnlyList<Poll> polls,
        IReadOnlyList<ApplicationUser> users,
        Random rng)
    {
        var likes = new List<PollLike>();
        foreach (var poll in polls)
        {
            int likeCount = rng.Next(0, 7);
            if (likeCount == 0) continue;

            // Кандидаты — все юзеры (включая автора, как в реальной жизни — автор может лайкнуть свой опрос).
            var likers = users.OrderBy(_ => rng.Next()).Take(likeCount);
            foreach (var u in likers)
            {
                likes.Add(new PollLike
                {
                    UserId = u.Id,
                    PollId = poll.Id,
                    CreatedAtUtc = poll.CreatedAtUtc.AddDays(rng.Next(0, 30)),
                });
            }
        }
        db.PollLikes.AddRange(likes);
        await db.SaveChangesAsync();
    }

    // ----------------------- COMMENT LIKES -----------------------

    private static async Task SeedCommentLikesAsync(
        ApplicationDbContext db,
        IReadOnlyList<ApplicationUser> users,
        Random rng)
    {
        // Перечитываем комментарии из БД — они уже сохранены, есть Id.
        var allComments = await db.Comments.AsNoTracking().ToListAsync();
        if (allComments.Count == 0) return;

        var likes = new List<CommentLike>();
        foreach (var comment in allComments)
        {
            if (rng.NextDouble() > 0.30) continue; // ~30% комментов получают лайки

            int count = rng.Next(1, 5);
            var likers = users.OrderBy(_ => rng.Next()).Take(count);
            foreach (var u in likers)
            {
                likes.Add(new CommentLike
                {
                    UserId = u.Id,
                    CommentId = comment.Id,
                    CreatedAtUtc = comment.CreatedAtUtc.AddHours(rng.Next(1, 48)),
                });
            }
        }
        db.CommentLikes.AddRange(likes);
        await db.SaveChangesAsync();
    }

    // ----------------------- SUBSCRIPTIONS -----------------------

    private static async Task SeedSubscriptionsAsync(
        ApplicationDbContext db,
        IReadOnlyList<ApplicationUser> users,
        ApplicationUser admin,
        Random rng)
    {
        int target = rng.Next(15, 26);

        // Подписчики — обычные user01..user10; цели — те же + admin (на admin тоже подписываемся).
        var subscribers = users;
        var targets = users.Concat(new[] { admin }).ToList();

        var pairs = new HashSet<(string sub, string tgt)>();
        var subs = new List<UserSubscription>();
        int safety = 0;

        while (pairs.Count < target && safety++ < 1000)
        {
            var sub = subscribers[rng.Next(subscribers.Count)];
            var tgt = targets[rng.Next(targets.Count)];
            if (sub.Id == tgt.Id) continue;
            if (!pairs.Add((sub.Id, tgt.Id))) continue;

            subs.Add(new UserSubscription
            {
                SubscriberId = sub.Id,
                TargetUserId = tgt.Id,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 60)),
            });
        }

        db.UserSubscriptions.AddRange(subs);
        await db.SaveChangesAsync();
    }

    // ----------------------- REPORTS -----------------------

    private static async Task SeedReportsAsync(
        ApplicationDbContext db,
        IReadOnlyList<Poll> polls,
        IReadOnlyList<ApplicationUser> users,
        string adminId,
        Random rng)
    {
        var activeUsers = users.Take(9).ToList(); // user01..user09 — репортеры
        var reportablePolls = polls.Where(p => p.Status != PollStatus.Draft).ToList();
        var existingComments = await db.Comments.AsNoTracking().Take(20).ToListAsync();

        var reports = new List<Report>();

        // 2 жалобы на Poll
        for (int i = 0; i < 2 && reportablePolls.Count > 0; i++)
        {
            var poll = reportablePolls[rng.Next(reportablePolls.Count)];
            var reporter = activeUsers.First(u => u.Id != poll.AuthorId);
            reports.Add(new Report
            {
                TargetType = ReportTargetType.Poll,
                TargetKey = poll.Id.ToString(),
                ReporterId = reporter.Id,
                Reason = i == 0 ? ReportReason.Spam : ReportReason.Misinformation,
                Comment = i == 0 ? "Похоже на спам-опрос с накруткой." : "Вводящие в заблуждение варианты ответов.",
                Status = ReportStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
            });
        }

        // 2 жалобы на Comment
        for (int i = 0; i < 2 && existingComments.Count > 0; i++)
        {
            var c = existingComments[rng.Next(existingComments.Count)];
            var reporter = activeUsers.First(u => u.Id != c.AuthorId);
            reports.Add(new Report
            {
                TargetType = ReportTargetType.Comment,
                TargetKey = c.Id.ToString(),
                ReporterId = reporter.Id,
                Reason = i == 0 ? ReportReason.Abuse : ReportReason.Nsfw,
                Comment = i == 0 ? "Оскорбления в адрес автора опроса." : "Неприемлемый контент.",
                Status = ReportStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
            });
        }

        // 2 жалобы на User (одна — на забаненного user10, для истории)
        var userTargets = new[]
        {
            (target: users[9], reason: ReportReason.Abuse, comment: "Систематически нарушает правила."),
            (target: users[7], reason: ReportReason.Other, comment: "Сомнительная активность."),
        };
        foreach (var (target, reason, comment) in userTargets)
        {
            var reporter = activeUsers.First(u => u.Id != target.Id);
            reports.Add(new Report
            {
                TargetType = ReportTargetType.User,
                TargetKey = target.Id,
                ReporterId = reporter.Id,
                Reason = reason,
                Comment = comment,
                Status = ReportStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 15)),
            });
        }

        // 2 последних помечаем как Reviewed (если есть).
        for (int i = reports.Count - 2; i < reports.Count; i++)
        {
            if (i < 0) continue;
            reports[i].Status = ReportStatus.Reviewed;
            reports[i].ReviewedAtUtc = DateTime.UtcNow.AddDays(-1);
            reports[i].ReviewedById = adminId;
            reports[i].AdminNote = "Рассмотрено в рамках тестового сидинга.";
        }

        db.Reports.AddRange(reports);
        await db.SaveChangesAsync();
    }

    // ----------------------- APPEALS -----------------------

    private static async Task SeedAppealsAsync(
        ApplicationDbContext db,
        ApplicationUser bannedUser,
        string adminId)
    {
        var pending = new Appeal
        {
            UserId = bannedUser.Id,
            BanReasonSnapshot = bannedUser.BanReason,
            BanCreatedAtSnapshot = bannedUser.BannedAt,
            Message = "Прошу пересмотреть бан — считаю, что блокировка была ошибочной. Готов соблюдать правила сообщества.",
            Status = AppealStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
        };

        var rejected = new Appeal
        {
            UserId = bannedUser.Id,
            BanReasonSnapshot = bannedUser.BanReason,
            BanCreatedAtSnapshot = bannedUser.BannedAt,
            Message = "Первая попытка обжалования бана. Не считаю свои действия нарушением.",
            Status = AppealStatus.Rejected,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
            ReviewedAtUtc = DateTime.UtcNow.AddDays(-4),
            ReviewedById = adminId,
            AdminResponse = "Нарушения подтверждены логами. Апелляция отклонена.",
        };

        db.Appeals.AddRange(pending, rejected);
        await db.SaveChangesAsync();
    }

    // ----------------------- FIXTURES -----------------------

    private record PollFixture(string Title, string? Description, string[] Options);

    private static List<PollFixture> PollFixtures() => new()
    {
        new("Какой язык программирования вы предпочитаете?",
            "Опрос для выбора стека на учебный проект.",
            new[] { "C#", "Python", "JavaScript", "Go", "Rust" }),

        new("Лучшая IDE для .NET?",
            "JetBrains Rider или Visual Studio?",
            new[] { "Visual Studio", "Rider", "VS Code", "Neovim" }),

        new("Любимый жанр кино",
            null,
            new[] { "Драма", "Комедия", "Триллер", "Фантастика" }),

        new("Где провести летний отпуск?",
            "Голосуйте за направление — выберем коллективно.",
            new[] { "Горы", "Море", "Город", "Деревня" }),

        new("Любимая кухня мира",
            null,
            new[] { "Итальянская", "Японская", "Грузинская", "Мексиканская", "Русская" }),

        new("Утро vs Вечер",
            "Когда вам продуктивнее работать?",
            new[] { "Утром", "Вечером", "Ночью" }),

        new("Питомец или нет?",
            "Социологический опрос для исследования.",
            new[] { "Кошка", "Собака", "Рыбки", "Нет питомца" }),

        new("Какой фреймворк выбрать для фронта?",
            null,
            new[] { "React", "Vue", "Angular", "Svelte" }),

        new("Книга или фильм?",
            "Если есть экранизация — что лучше?",
            new[] { "Книга всегда лучше", "Фильм", "Зависит от автора" }),

        new("Чай или кофе по утрам?",
            null,
            new[] { "Кофе", "Чай", "Вода", "Ничего" }),

        new("Лучший способ изучения нового языка",
            "Поделитесь опытом — что реально работает.",
            new[] { "Курсы", "Учебники", "Сериалы", "Общение с носителем" }),

        new("Удалёнка или офис?",
            "Опрос для HR-исследования.",
            new[] { "Полная удалёнка", "Гибрид", "Офис" }),

        new("Лучший сезон",
            null,
            new[] { "Зима", "Весна", "Лето", "Осень" }),

        new("Стоит ли переходить на электромобиль?",
            "Анонимный опрос — ответы видны только в агрегированном виде.",
            new[] { "Да", "Пока рано", "Никогда" }),

        new("Какие хобби помогают вам отдыхать?",
            "Можно выбрать несколько вариантов.",
            new[] { "Спорт", "Чтение", "Игры", "Прогулки", "Музыка" }),
    };

    private static string[] RootCommentTemplates() => new[]
    {
        "Согласен с автором, отличный опрос!",
        "А мне кажется, выбор не полный — не хватает важного варианта.",
        "Голосовал за второй вариант, но всё спорно.",
        "Не хватает варианта про что-то нейтральное.",
        "Поделюсь опытом: в нашей команде сложилось иначе.",
        "Очень интересная тема, спасибо за опрос!",
        "Можно было сделать MultipleChoice, выбор сложный.",
        "Где можно посмотреть итоговую статистику?",
        "Не уверен в актуальности темы.",
        "Подписался на автора, жду новых опросов.",
        "Опрос полезный для обучения.",
        "По-моему, тут ответ очевиден.",
        "Интересно, как изменится распределение через неделю.",
        "Голосовал не задумываясь — первый вариант ближе.",
    };

    private static string[] ReplyCommentTemplates() => new[]
    {
        "А вот тут не соглашусь — аргументы у меня другие.",
        "Поддерживаю, у нас такая же ситуация.",
        "А что если посмотреть с другой стороны?",
        "Спасибо за пояснение!",
        "Не убедил, но позицию понимаю.",
        "Тоже об этом думал, плюсую.",
    };
}
