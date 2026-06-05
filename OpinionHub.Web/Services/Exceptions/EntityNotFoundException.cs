namespace OpinionHub.Web.Services.Exceptions;

/// <summary>
/// Бросается сервисом, когда запрошенная сущность не найдена. Глобальный
/// <see cref="OpinionHub.Web.Filters.DomainExceptionFilter"/> маппит это на 404,
/// чтобы контроллеры не дублировали проверки и try/catch.
/// </summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message) { }
}
