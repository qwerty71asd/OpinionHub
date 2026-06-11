using System.ComponentModel.DataAnnotations;

namespace OpinionHub.Web.ViewModels;

public class CreateWithAiInputVm
{
    [Required(ErrorMessage = "Опишите тему опроса")]
    [StringLength(200, ErrorMessage = "Максимум 200 символов")]
    public string Topic { get; set; } = string.Empty;

    [Range(2, 6, ErrorMessage = "От 2 до 6 вариантов")]
    public int OptionCount { get; set; } = 4;
}

public record GeneratePollAiRequest(string Topic, int OptionCount);
