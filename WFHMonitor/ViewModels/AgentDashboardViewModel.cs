namespace WFHMonitor.ViewModels;

public class AgentDashboardViewModel
{
    public string CeoName { get; set; } = "CEO";
    public string CeoEmail { get; set; } = string.Empty;
    public int TeamCount { get; set; }
    public int AgentCount { get; set; }
    public int EmployeeCount { get; set; }
    public int WorkingAgentCount { get; set; }
    public int ActiveTaskCount { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public List<AgentOfficeAvatarViewModel> Agents { get; set; } = new();
    public List<OfficeEmployeeAvatarViewModel> Employees { get; set; } = new();
}

public class AgentOfficeAvatarViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TeamName { get; set; } = "Unassigned";
    public string WorkspaceName { get; set; } = string.Empty;
    public bool IsWorking { get; set; }
    public int ActiveTaskCount { get; set; }
    public string TaskSummary { get; set; } = "Idle";
    public string AccentColor { get; set; } = "#22d3ee";
    public int IdleLeftPct { get; set; }
    public int IdleTopPct { get; set; }
    public int WorkLeftPct { get; set; }
    public int WorkTopPct { get; set; }
}

public class OfficeEmployeeAvatarViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
    public string TeamName { get; set; } = "Unassigned";
    public string AccentColor { get; set; } = "#60a5fa";
    public int LeftPct { get; set; }
    public int TopPct { get; set; }
}
