using System.Reflection;
using WFHMonitor.Controllers;

namespace WFHMonitor.Tests;

public class ChangeRequestControllerTests
{
    [Theory]
    [InlineData("/ChangeRequest/Features?projectId=42", 42, "/ChangeRequest/Features?projectId=42")]
    [InlineData(null, 42, "/ChangeRequest/Features?projectId=42")]
    [InlineData(null, null, "/ChangeRequest/Features")]
    [InlineData("https://evil.example/steal", 42, "/ChangeRequest/Features?projectId=42")]
    public void ResolveFeatureCancelUrl_ReturnsExpectedSafeFallback(string? returnUrl, int? projectId, string expected)
    {
        var method = typeof(ChangeRequestController)
            .GetMethod("ResolveFeatureCancelUrl", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, new object?[] { returnUrl, projectId })!;

        Assert.Equal(expected, result);
    }
}
