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
    public List<string> SelectedRoles { get; set; } = new();
}
