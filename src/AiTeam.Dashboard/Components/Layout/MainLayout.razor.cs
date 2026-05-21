using System.Reflection;

namespace AiTeam.Dashboard.Components.Layout;

public partial class MainLayout
{
    protected string AppVersion
    {
        get
        {
            // Stage 83 v3 Bug 7 修根因：改 GetEntryAssembly()（ASP.NET Core 慣例 = 執行 Program 的 Dashboard.dll）
            // GetExecutingAssembly() 在 Razor partial class context 可能讀錯 generated.cs assembly / 不是 Dashboard entry assembly /
            // 導致顯示舊版本 v3.74.0 vs 真實 v3.75.0 — Stage 26 集中版本管理紀律對齊。
            var version = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
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
