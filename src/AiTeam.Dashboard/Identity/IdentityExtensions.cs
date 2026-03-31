using Microsoft.AspNetCore.Identity;

namespace AiTeam.Dashboard.Identity;

/// <summary>
/// 啟動時確保 Owner 帳號存在，帳號密碼從設定讀取，避免寫死。
/// </summary>
public static class IdentityExtensions
{
    public static async Task EnsureAdminUserAsync(
        this IServiceProvider services,
        IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var adminEmail    = configuration["Dashboard:AdminEmail"]    ?? "";
        var adminPassword = configuration["Dashboard:AdminPassword"] ?? "";

        Console.WriteLine($"[EnsureAdminUser] AdminEmail={adminEmail}, PasswordLength={adminPassword.Length}");

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            Console.WriteLine("[EnsureAdminUser] 設定缺失，跳過管理員建立");
            return;
        }

        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
        {
            Console.WriteLine("[EnsureAdminUser] 管理員帳號已存在");
            return;
        }

        Console.WriteLine("[EnsureAdminUser] 建立管理員帳號中...");
        var user   = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, adminPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            Console.WriteLine($"[EnsureAdminUser] 建立失敗：{errors}");
            throw new InvalidOperationException($"管理員帳號建立失敗：{errors}");
        }

        Console.WriteLine("[EnsureAdminUser] 管理員帳號建立成功");
    }
}
