using System.Reflection;

namespace AiTeam.Dashboard.Components.Layout;

public partial class MainLayout
{
    protected string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            return string.IsNullOrWhiteSpace(version) ? string.Empty : $"v{version}";
        }
    }
}
