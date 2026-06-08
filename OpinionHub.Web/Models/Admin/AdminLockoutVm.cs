using System.ComponentModel.DataAnnotations;

namespace OpinionHub.Web.Models.Admin;

public class AdminLockoutVm
{
    [Required(ErrorMessage = "Это обязательное поле")]
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    [Display(Name = "Бессрочно")]
    public bool Permanent { get; set; }

    [Range(1, 525600)] // до года в минутах
    [Display(Name = "Минут")]
    public int Minutes { get; set; } = 60;

    [Display(Name = "Причина блокировки")]
    [StringLength(1000, ErrorMessage = "Причина не должна превышать 1000 символов")]
    public string? Reason { get; set; }
}
