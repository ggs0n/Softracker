using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public static class OnboardingStepKeys
{
    public const string AddProject = "add-project";
    public const string AddGitHubIntegration = "add-github-integration";
    public const string Done = "done";

    public static readonly string[] Ordered = [AddProject, AddGitHubIntegration, Done];
}

public class UserOnboardingState
{
    [Key]
    [ForeignKey(nameof(User))]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public bool IsDismissed { get; set; }
    public bool IsCompleted { get; set; }

    [MaxLength(50)]
    public string LastSeenStepKey { get; set; } = OnboardingStepKeys.AddProject;

    public DateTime? DismissedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
