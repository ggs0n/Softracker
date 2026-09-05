using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WFHMonitor.Infrastructure.Spa;

public sealed class SpaApiCompatibilityFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            if (context.Result is ViewResult
                && TryGetLegacySpaPath(context, out var spaPath))
            {
                context.Result = new RedirectResult(spaPath);
            }
            return;
        }

        var controller = context.Controller as Controller;
        var errors = controller?.ModelState
            .Where(pair => pair.Value?.Errors.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage)
                    .ToArray())
            ?? new Dictionary<string, string[]>();

        var success = ReadTempData(controller, "Success");
        var error = ReadTempData(controller, "Error");

        switch (context.Result)
        {
            case ViewResult view:
                context.Result = new JsonResult(new SpaApiEnvelope(
                    view.Model,
                    errors,
                    success,
                    error,
                    null))
                {
                    StatusCode = errors.Count > 0 ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK
                };
                break;

            case PartialViewResult partial:
                context.Result = new JsonResult(new SpaApiEnvelope(
                    partial.Model,
                    errors,
                    success,
                    error,
                    null))
                {
                    StatusCode = errors.Count > 0 ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK
                };
                break;

            case RedirectToActionResult redirectToAction:
                context.Result = new JsonResult(new SpaApiEnvelope(
                    null,
                    errors,
                    success,
                    error,
                    new SpaRedirect(
                        redirectToAction.ControllerName,
                        redirectToAction.ActionName,
                        redirectToAction.RouteValues,
                        null)));
                break;

            case LocalRedirectResult localRedirect:
                context.Result = new JsonResult(new SpaApiEnvelope(
                    null,
                    errors,
                    success,
                    error,
                    new SpaRedirect(null, null, null, localRedirect.Url)));
                break;

            case RedirectResult redirect:
                context.Result = new JsonResult(new SpaApiEnvelope(
                    null,
                    errors,
                    success,
                    error,
                    new SpaRedirect(null, null, null, redirect.Url)));
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static string? ReadTempData(Controller? controller, string key)
    {
        return controller?.TempData.TryGetValue(key, out var value) == true
            ? value?.ToString()
            : null;
    }

    private static bool TryGetLegacySpaPath(
        ResultExecutingContext context,
        out string spaPath)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? "Index";
        var id = context.RouteData.Values["id"]?.ToString()
                 ?? context.HttpContext.Request.Query["id"].ToString();

        spaPath = (controller, action) switch
        {
            ("Admin", "Index") => "/app/dashboard",
            ("Admin", "Employees") => "/app/employees",
            ("Agent", "Index") => "/app/agents",
            ("Auth", "Login") => "/app/login",
            ("Auth", "Register") => "/app/register",
            ("Auth", "AccessDenied") => "/app/denied",
            ("Bug", "Index") => "/app/bugs",
            ("Bug", "Create") => "/app/bugs/new",
            ("Bug", "Details") when !string.IsNullOrWhiteSpace(id) => $"/app/bugs/{Uri.EscapeDataString(id)}",
            ("Bug", "Edit") when !string.IsNullOrWhiteSpace(id) => $"/app/bugs/{Uri.EscapeDataString(id)}/edit",
            ("ChangeRequest", "Index") => "/app/projects",
            ("ChangeRequest", "Features") => "/app/features",
            ("ChangeRequest", "Create") => "/app/projects/new",
            ("ChangeRequest", "CreateFeature") => "/app/features/new",
            ("ChangeRequest", "Details") when !string.IsNullOrWhiteSpace(id) => $"/app/projects/{Uri.EscapeDataString(id)}",
            ("ChangeRequest", "Edit") when !string.IsNullOrWhiteSpace(id) => $"/app/projects/{Uri.EscapeDataString(id)}/edit",
            ("ChangeRequest", "FeatureDetails") => FeaturePath(context, false),
            ("ChangeRequest", "EditFeature") => FeaturePath(context, true),
            ("Developer", "Summary") => "/app/developer",
            ("Monitor", "Index") => "/app/monitor",
            ("Onboarding", "Welcome") => "/app/onboarding",
            ("Onboarding", "SetupTeam") => "/app/onboarding/team",
            ("Payment", "Index") => "/app/subscription",
            ("Qa", "Index") => "/app/qa",
            ("Qa", "Details") when !string.IsNullOrWhiteSpace(id) => $"/app/qa/{Uri.EscapeDataString(id)}",
            ("Settings", "Index") => "/app/settings",
            ("Team", "Index") => "/app/team",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(spaPath);
    }

    private static string FeaturePath(ResultExecutingContext context, bool edit)
    {
        var featureId = context.HttpContext.Request.Query["featureId"].ToString();
        if (string.IsNullOrWhiteSpace(featureId))
            return "/app/features";

        var suffix = edit ? "/edit" : string.Empty;
        return $"/app/features/{Uri.EscapeDataString(featureId)}{suffix}";
    }
}

public sealed record SpaApiEnvelope(
    object? Data,
    IReadOnlyDictionary<string, string[]> Errors,
    string? Success,
    string? Error,
    SpaRedirect? Redirect);

public sealed record SpaRedirect(
    string? Controller,
    string? Action,
    object? RouteValues,
    string? Url);
