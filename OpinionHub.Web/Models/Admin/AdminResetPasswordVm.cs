using System.ComponentModel.DataAnnotations;

namespace OpinionHub.Web.Models.Admin;

public class AdminResetPasswordVm
{
    [Required(ErrorMessage = "Это обязательное поле")]
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Это обязательное поле")]
    [MinLength(6)]
    [DataType(DataType.Password)]
    [Display(Name = "Новый пароль")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Это обязательное поле")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Пароли не совпадают")]
    [Display(Name = "Повтор пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
