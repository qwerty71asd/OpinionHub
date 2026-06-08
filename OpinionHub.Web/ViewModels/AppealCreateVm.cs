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
    [StringLength(2000, MinimumLength = 50, ErrorMessage = "Слишком короткое сообщение — напишите хотя бы 50 символов (максимум 2000).")]
    [Display(Name = "Сообщение")]
    public string Message { get; set; } = string.Empty;
}
