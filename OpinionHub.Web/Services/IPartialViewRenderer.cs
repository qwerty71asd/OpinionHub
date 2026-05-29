namespace OpinionHub.Web.Services;

/// <summary>
/// Рендерит PartialView в строку — нужно, чтобы переиспользовать тот же Razor-шаблон,
/// что и MVC-контроллеры, при broadcast'е через SignalR (где нет привычного
/// PartialViewResult-pipeline).
/// </summary>
public interface IPartialViewRenderer
{
    /// <param name="viewName">Короткое имя партиала (например, "_Comment"). ViewEngine
    /// ищет его по обычным конвенциям ASP.NET Core.</param>
    /// <param name="model">Модель, которая попадёт в Razor через @model.</param>
    /// <param name="httpContext">Контекст текущего запроса — нужен ViewEngine для
    /// resolving путей и Razor для @inject зависимостей.</param>
    Task<string> RenderAsync<TModel>(string viewName, TModel model, HttpContext httpContext);
}
