using Microsoft.AspNetCore.Identity;

namespace OpinionHub.Web.Services;

/// <summary>
/// Русские сообщения об ошибках Identity (регистрация, смена пароля, роли и т.д.).
/// </summary>
public class RuIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Произошла неизвестная ошибка." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Пользователь с таким логином уже существует." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Пользователь с таким email уже существует." };

    public override IdentityError InvalidUserName(string userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Некорректный логин." };

    public override IdentityError InvalidEmail(string email) =>
        new() { Code = nameof(InvalidEmail), Description = "Некорректный email." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Пароль должен содержать не менее {length} символов." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Пароль должен содержать хотя бы одну цифру." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Пароль должен содержать хотя бы одну строчную букву." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Пароль должен содержать хотя бы одну заглавную букву." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Пароль должен содержать хотя бы один спецсимвол." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Неверный пароль." };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Некорректный токен." };
}
