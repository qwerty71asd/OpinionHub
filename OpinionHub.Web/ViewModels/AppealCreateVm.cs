using System.ComponentModel.DataAnnotations;

namespace OpinionHub.Web.ViewModels;

public class AppealCreateVm
{
    [Required(ErrorMessage = "Укажите ваш логин.")]
    [Display(Name = "Логин")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите пароль для подтверждения.")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опишите ситуацию.")]
    [StringLength(2000, MinimumLength = 50, ErrorMessage = "Сообщение от 50 до 2000 символов.")]
    [Display(Name = "Сообщение")]
    public string Message { get; set; } = string.Empty;
}
