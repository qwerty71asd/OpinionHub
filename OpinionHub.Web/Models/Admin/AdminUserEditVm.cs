using System.ComponentModel.DataAnnotations;

namespace OpinionHub.Web.Models.Admin;

public class AdminUserEditVm
{
    [Required(ErrorMessage = "Это обязательное поле")]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Это обязательное поле")]
    [Display(Name = "Логин")]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Телефон")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Email подтвержден")]
    public bool EmailConfirmed { get; set; }

    [Display(Name = "Заблокирован до (UTC)")]
    public DateTimeOffset? LockoutEndUtc { get; set; }

    public List<string> AvailableRoles { get; set; } = new();

    /// <summary>Одна выбранная роль (radio). Раньше был List&lt;string&gt; — но прав у Participant и Author
    /// не отличалось, поэтому ролей по дизайну две (User, Admin) и одновременно может быть только одна.</summary>
    [Display(Name = "Роль")]
    public string? SelectedRole { get; set; }

    // === Подробная админка: контент пользователя для контекста модерации ===

    public List<AdminUserPollVm> RecentPolls { get; set; } = new();
    public List<AdminUserCommentVm> RecentComments { get; set; } = new();

    /// <summary>Сколько жалоб подано на этого пользователя (в любой статус).</summary>
    public int ReportsAgainstCount { get; set; }
}

public class AdminUserPollVm
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AdminUserCommentVm
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public Guid PollId { get; set; }
    public string PollTitle { get; set; } = string.Empty;
}
