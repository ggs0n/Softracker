using WFHMonitor.Services;

namespace WFHMonitor.Tests;

public class CodeCleanupTests
{
    [Theory]
    [InlineData("https://github.com/openai/example", "openai", "example", null)]
    [InlineData("https://github.com/openai/example.git", "openai", "example", null)]
    [InlineData("https://github.com/openai/example/tree/feature/demo", "openai", "example", "feature/demo")]
    public void GitHubRepositoryUrlParser_ParsesSupportedUrls(
        string url,
        string expectedOwner,
        string expectedRepository,
        string? expectedBranch)
    {
        var parsed = GitHubRepositoryUrlParser.TryParse(url, out var owner, out var repository, out var branch);

        Assert.True(parsed);
        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepository, repository);
        Assert.Equal(expectedBranch, branch);
    }

    [Theory]
    [InlineData("TC-2026-0042", 42)]
    [InlineData("invalid", 0)]
    [InlineData(null, 0)]
    public void QaTestNumberParser_ReturnsExpectedSequence(string? testNumber, int expected)
    {
        Assert.Equal(expected, QaTestNumberParser.ParseSequence(testNumber));
    }
}
