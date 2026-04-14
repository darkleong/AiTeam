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
            // 截掉 commit hash（格式：3.11.0+abc123）
            if (version is not null)
            {
                var plusIdx = version.IndexOf('+');
                if (plusIdx >= 0) version = version[..plusIdx];
            }
            return string.IsNullOrWhiteSpace(version) ? string.Empty : $"v{version}";
        }
    }
}
