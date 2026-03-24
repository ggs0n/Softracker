using System.Reflection;
using System.ComponentModel.DataAnnotations;
using WFHMonitor.Models;

namespace WFHMonitor.Tests;

public class ChangeRequestConcurrencyRegressionTests
{
    [Fact]
    public void ChangeRequest_UsesTimestampConcurrencyToken()
    {
        var property = typeof(ChangeRequest).GetProperty(nameof(ChangeRequest.RowVersion));
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<TimestampAttribute>());
    }

    [Fact]
    public void EditView_PostsHiddenRowVersion()
    {
        var viewPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Views", "ChangeRequest", "Edit.cshtml");
        var content = File.ReadAllText(Path.GetFullPath(viewPath));

        Assert.Contains("<input type=\"hidden\" asp-for=\"RowVersion\" />", content);
    }

    [Fact]
    public void EditAction_HandlesConcurrencyException()
    {
        var controllerPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Controllers", "ChangeRequestController.cs");
        var content = File.ReadAllText(Path.GetFullPath(controllerPath));

        Assert.Contains("DbUpdateConcurrencyException", content);
        Assert.Contains("This project was updated by another user. Reload the page and apply your changes again.", content);
        Assert.Contains("Convert.ToBase64String(cr.RowVersion)", content);
    }
}
