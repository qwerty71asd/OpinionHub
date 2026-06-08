using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.ViewModels;

public class EditPollViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Это обязательное поле")]
    [StringLength(200, ErrorMessage = "Максимум 200 символов")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Максимум 1000 символов")]
    public string? Description { get; set; }

    public PollType PollType { get; set; }
    public VisibilityType VisibilityType { get; set; }
    public AudienceType AudienceType { get; set; } = AudienceType.Everyone;
    public bool CanChangeVote { get; set; }
    public bool IsAnonymousAuthor { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public List<string> AllowedUserIds { get; set; } = new();

    public bool PublishNow { get; set; }

    public string? ExistingCoverPath { get; set; }
    public IFormFile? CoverImage { get; set; }
    public bool RemoveCover { get; set; }

    [MinLength(2, ErrorMessage = "Нужно минимум 2 варианта")]
    public List<EditPollOptionVm> Options { get; set; } = new();

    public List<ExistingAttachmentVm> ExistingAttachments { get; set; } = new();
    public List<Guid> RemoveAttachmentIds { get; set; } = new();
    public List<IFormFile>? AttachedFiles { get; set; }
}

public class EditPollOptionVm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Текст варианта обязателен")]
    public string Text { get; set; } = string.Empty;

    public string? ExistingImagePath { get; set; }
    public IFormFile? Image { get; set; }
    public bool RemoveImage { get; set; }
}

public class ExistingAttachmentVm
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
}
