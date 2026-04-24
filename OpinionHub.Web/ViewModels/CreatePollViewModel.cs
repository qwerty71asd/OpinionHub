using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; // Обязательно для IFormFile
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

    /// <summary>
    /// Список UserId выбранных участников (если AudienceType == SelectedUsers).
    /// Приходит из формы как набор hidden-input с одинаковым именем.
    /// </summary>
    public List<string> AllowedUserIds { get; set; } = new();

    public bool PublishNow { get; set; }

    // --- НОВЫЕ ПОЛЯ ДЛЯ МЕДИА ---

    public IFormFile? CoverImage { get; set; }

    // Список прикрепленных файлов (вложения)
    public List<IFormFile>? AttachedFiles { get; set; }

    // Обновленный список вариантов ответа (Текст + Картинка)
    [MinLength(2, ErrorMessage = "Нужно минимум 2 варианта")]
    public List<CreatePollOptionVm> Options { get; set; } = new()
    {
        new CreatePollOptionVm(),
        new CreatePollOptionVm()
    };
}

// Вспомогательный класс для вариантов ответа
public class CreatePollOptionVm
{
    [Required(ErrorMessage = "Текст варианта обязателен")]
    public string Text { get; set; } = string.Empty;

    public IFormFile? Image { get; set; }
}