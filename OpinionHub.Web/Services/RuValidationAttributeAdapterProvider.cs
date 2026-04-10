using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace OpinionHub.Web.Services;

/// <summary>
/// Делает сообщения DataAnnotations (Required, EmailAddress и т.п.) человеко-понятными по-русски.
/// Важно: MVC добавляет "неявный Required" для non-nullable string — это тоже проходит через этот адаптер.
/// </summary>
public sealed class RuValidationAttributeAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _defaultProvider = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
    {
        // Если разработчик уже указал ErrorMessage/ресурс — не трогаем.
        var hasCustomMessage =
            !string.IsNullOrWhiteSpace(attribute.ErrorMessage) ||
            !string.IsNullOrWhiteSpace(attribute.ErrorMessageResourceName);

        if (!hasCustomMessage)
        {
            switch (attribute)
            {
                case RequiredAttribute:
                    attribute.ErrorMessage = "Это обязательное поле";
                    break;

                case EmailAddressAttribute:
                    attribute.ErrorMessage = "Некорректный email";
                    break;

                case StringLengthAttribute sl when sl.MinimumLength > 0:
                    attribute.ErrorMessage = $"Длина поля должна быть от {sl.MinimumLength} до {sl.MaximumLength} символов";
                    break;

                case StringLengthAttribute sl:
                    attribute.ErrorMessage = $"Максимальная длина поля — {sl.MaximumLength} символов";
                    break;
            }
        }

        return _defaultProvider.GetAttributeAdapter(attribute, stringLocalizer);
    }
}
