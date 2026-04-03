
```csharp
using System.Reflection;

namespace AiTeam.Bot.Services
{
    public interface IDevAgentService
    {
        string GetVersion();
    }

    public class DevAgentService : IDevAgentService
    {
        public string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? version.ToString() : "1.0.0.0";
        }
    }
}
```