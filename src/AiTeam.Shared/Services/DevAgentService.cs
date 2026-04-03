
```csharp
using System;
using System.Reflection;

namespace AiTeam.Shared.Services
{
    public class DevAgentService
    {
        private readonly string _version;

        public DevAgentService()
        {
            _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        public DevAgentService(string version)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public string GetVersion()
        {
            return _version;
        }
    }
}
```