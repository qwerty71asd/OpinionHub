using System.ComponentModel.DataAnnotations;

namespace OpinionHub.Web.Models.Admin;

public class AdminUserCreateVm
{
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

    [Required(ErrorMessage = "Это обязательное поле")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Это обязательное поле")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    [Display(Name = "Повтор пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Роли")]
    public List<string> AvailableRoles { get; set; } = new();

    public List<string> SelectedRoles { get; set; } = new();
}
