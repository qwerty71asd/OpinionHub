using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace OpinionHub.Web.Services;

/// <summary>
/// Стандартный паттерн «render Razor PartialView in string» для .NET 8 MVC:
/// собираем минимальный ViewContext, отдаём ViewEngine'у короткое имя, рендерим в StringWriter.
/// Используется для broadcast'а готового HTML через SignalR (см. CommentsController.Create).
/// </summary>
public class PartialViewRenderer : IPartialViewRenderer
{
    private readonly ICompositeViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;

    public PartialViewRenderer(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
    }

    public async Task<string> RenderAsync<TModel>(string viewName, TModel model, HttpContext httpContext)
    {
        var actionContext = new ActionContext(
            httpContext,
            httpContext.GetRouteData() ?? new RouteData(),
            new ActionDescriptor());

        var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);
        if (!viewResult.Success)
        {
            var searched = viewResult.SearchedLocations is { } locs
                ? string.Join(", ", locs)
                : "(none)";
            throw new InvalidOperationException(
                $"PartialViewRenderer: view '{viewName}' not found. Searched: {searched}");
        }

        var viewData = new ViewDataDictionary<TModel>(
            new EmptyModelMetadataProvider(),
            new ModelStateDictionary())
        {
            Model = model
        };

        var tempData = new TempDataDictionary(httpContext, _tempDataProvider);

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            tempData,
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
