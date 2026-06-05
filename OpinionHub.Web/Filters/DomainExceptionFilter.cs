using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OpinionHub.Web.Services.Exceptions;

namespace OpinionHub.Web.Filters;

/// <summary>
/// Глобальный маппинг доменных исключений на HTTP-статусы. Подключается через
/// <c>AddControllersWithViews(o =&gt; o.Filters.Add&lt;DomainExceptionFilter&gt;())</c> в Program.cs.
///
/// Для путей под <c>/api</c> возвращает JSON <see cref="ProblemDetails"/>; для остальных
/// (MVC-страниц) — <see cref="NotFoundResult"/> / <see cref="ForbidResult"/>, чтобы ASP.NET
/// сам отрендерил стандартную страницу или редирект на AccessDenied.
/// </summary>
public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (statusCode, title) = context.Exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Не найдено"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Доступ запрещён"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Доступ запрещён"),
            _ => (0, string.Empty),
        };

        if (statusCode == 0) return;

        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        var isApi = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);

        if (isApi)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = context.Exception.Message,
            })
            {
                StatusCode = statusCode,
            };
        }
        else
        {
            context.Result = statusCode == StatusCodes.Status404NotFound
                ? new NotFoundResult()
                : new ForbidResult();
        }

        context.ExceptionHandled = true;
    }
}
