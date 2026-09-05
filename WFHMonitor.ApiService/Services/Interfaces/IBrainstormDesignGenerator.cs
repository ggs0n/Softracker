using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IBrainstormDesignGenerator
{
    Task<BrainstormDesignBlueprint> GenerateAsync(
        BrainstormGenerateDesignRequest request,
        CancellationToken cancellationToken);
}
