using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class AdminEmployeesViewModel
{
    public List<EmployeeListItem> Employees { get; set; } = new();
    public RegisterViewModel NewEmployee { get; set; } = new();
}

public class EmployeeListItem
{
    public ApplicationUser Employee { get; set; } = null!;
    public string Role { get; set; } = "Employee";
}
