using System.ComponentModel.DataAnnotations;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.ViewModels;

public class ReportCreateVm
{
    [Required]
    public ReportTargetType TargetType { get; set; }

    [Required]
    public string TargetKey { get; set; } = string.Empty;

    [Required]
    public ReportReason Reason { get; set; }

    [StringLength(500, ErrorMessage = "Комментарий не длиннее 500 символов.")]
    public string? Comment { get; set; }

    /// <summary>Куда вернуться после отправки (URL).</summary>
    public string? ReturnUrl { get; set; }
}
