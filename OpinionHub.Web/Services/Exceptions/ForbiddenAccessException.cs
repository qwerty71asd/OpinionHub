namespace OpinionHub.Web.Services.Exceptions;

/// <summary>
/// Бросается сервисом, когда у текущего пользователя нет прав на действие над сущностью
/// (например, попытка опубликовать/удалить чужой опрос). Глобальный
/// <see cref="OpinionHub.Web.Filters.DomainExceptionFilter"/> маппит это на 403.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}
