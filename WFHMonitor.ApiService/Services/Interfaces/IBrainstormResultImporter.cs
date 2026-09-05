using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IBrainstormResultImporter
{
    BrainstormDesignBlueprint Import(string rawResult);
}
