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
    [Display(Name = "Минут (если не бессрочно)")]
    public int Minutes { get; set; } = 60;
}
