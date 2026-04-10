using System.ComponentModel.DataAnnotations;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.ViewModels;

public class CreatePollViewModel
{
    [Required(ErrorMessage = "Это обязательное поле")]
    [StringLength(200, ErrorMessage = "Максимум 200 символов")]
    public string Title { get; set; } = string.Empty;

    public PollType PollType { get; set; }
    public VisibilityType VisibilityType { get; set; }
    public AudienceType AudienceType { get; set; } = AudienceType.Everyone;
    public bool CanChangeVote { get; set; }

    public DateTime? EndDateUtc { get; set; }

    [MinLength(2, ErrorMessage = "Нужно минимум 2 варианта")]
    public List<string> Options { get; set; } = new() { "", "" };

    /// <summary>
    /// Список UserId выбранных участников (если AudienceType == SelectedUsers).
    /// Приходит из формы как набор hidden-input с одинаковым именем.
    /// </summary>
    public List<string> AllowedUserIds { get; set; } = new();

    public bool PublishNow { get; set; }
}
