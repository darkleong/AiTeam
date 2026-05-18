# EF Core 規範

## 避免 N+1 查詢

```csharp
// ❌ N+1 問題
var users = _dbContext.Users.ToList();
foreach (var user in users)
{
    var orders = _dbContext.Orders.Where(o => o.UserId == user.Id).ToList();
}

// ✅ 使用 Include
var users = await _dbContext.Users
    .Include(u => u.Orders)
    .ThenInclude(o => o.OrderItems)
    .ToListAsync();
```

## 伺服器端評估

```csharp
// ❌ 客戶端評估（低效）
var users = _dbContext.Users.ToList()
    .Where(u => u.Name.Contains(searchTerm)).ToList();

// ✅ 伺服器端評估
var users = await _dbContext.Users
    .Where(u => u.Name.Contains(searchTerm))
    .ToListAsync();
```

## 使用 Select 投影

```csharp
// ✅ 只取需要的欄位
var userDtos = await _dbContext.Users
    .Select(u => new UserDto
    {
        Id = u.Id,
        Name = u.Name,
        OrderCount = u.Orders.Count
    })
    .ToListAsync();
```

## AsNoTracking 規則

- 只讀查詢 → 使用 `AsNoTracking()`（提升效能）
- 需要修改的查詢 → 不使用 `AsNoTracking()`

## 異常處理

本專案使用 **PostgreSQL（Npgsql）**，例外型別為 `PostgresException`，不是 SQL Server 的 `SqlException`：

```csharp
using Npgsql;

catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
{
    _dbContext.ChangeTracker.Clear();  // 必須清除追蹤狀態
    return pgEx.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation     => BadRequest("唯一鍵衝突"),
        PostgresErrorCodes.ForeignKeyViolation => BadRequest("外鍵約束失敗"),
        PostgresErrorCodes.NotNullViolation    => BadRequest("必填欄位不得為空"),
        _                                       => BadRequest(pgEx.MessageText),
    };
}
catch (Exception ex)
{
    _dbContext.ChangeTracker.Clear();  // 必須清除追蹤狀態
    return BadRequest(ex.Message);
}
```

## PostgreSQL nullable unique + race-safe DbSeeder pattern

> Stage 67 follow-up commit `6fd9472` 教訓 — Bot + Dashboard 並行 SeedAsync race + PostgreSQL `NULL ≠ NULL` 雙重雷三層修根因（[Stage 67 Roadmap](../planning/Stage_67_Roadmap.md)）。

### 1. PostgreSQL `NULL ≠ NULL` unique 語義

PostgreSQL 標準 unique constraint 對 nullable 欄位**不阻擋多筆 NULL**（NULL 不參與唯一性比較）— 兩個 race winner 都 commit 成功 → DB duplicate row。

**修法：拆 partial unique index**（兩條 — NOT NULL 群組 + NULL 群組分開 enforce）：

```csharp
// ❌ 標準 unique 對 (ProjectId, Name) — ProjectId=null 多筆都過
modelBuilder.Entity<Talent>()
    .HasIndex(t => new { t.ProjectId, t.Name })
    .IsUnique();

// ✅ 拆 partial unique（含 NULL 群組明確 enforce）
modelBuilder.Entity<Talent>()
    .HasIndex(t => new { t.ProjectId, t.Name })
    .IsUnique()
    .HasFilter("\"ProjectId\" IS NOT NULL");

modelBuilder.Entity<Talent>()
    .HasIndex(t => t.Name)
    .IsUnique()
    .HasFilter("\"ProjectId\" IS NULL");
```

### 2. Bot + Dashboard 並行 SeedAsync race

兩個 process 同時跑 seed → 都看 existing=null → 都 Add → 都 SaveChanges → 兩個 row 都進 DB（PostgreSQL `NULL ≠ NULL` 雷不阻）。

**修法：per-row SaveChanges + catch DbUpdateException + Entity detach**

```csharp
foreach (var seed in seeds)
{
    var existing = await db.Talents.FirstOrDefaultAsync(t => t.Name == seed.Name);
    if (existing is not null) continue;

    db.Talents.Add(seed);
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException) // race loser — 對手已先 commit
    {
        db.Entry(seed).State = EntityState.Detached; // 還原 EF context state
    }
}

// 防禦性 dedupe — 萬一 race 漏網 / partial index 套用前歷史 row 也不爆
var byName = await db.Talents
    .GroupBy(t => t.Name)
    .Select(g => g.OrderBy(t => t.CreatedAt).First())
    .ToDictionaryAsync(t => t.Name);
```

### 3. DI lifecycle — `app.Build()` 前 register / DB migrate 後 seed

Singleton 直接持 `AppDbContext`（Scoped）會觸發 lifetime mismatch；`app.Build()` 前需註冊但 DB 還沒 migrate。**改用 `IServiceScopeFactory` factory pattern**：

```csharp
// Program.cs — register
builder.Services.AddSingleton<DbSeeder>();

// DbSeeder ctor 注入 IServiceScopeFactory（Singleton-safe）
public class DbSeeder(IServiceScopeFactory scopeFactory, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // ... seed logic
    }
}

// Program.cs — app.Build() 後 DB migrate 完才呼叫
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}
```

## SaveChangesAsync 原則

```csharp
// ✅ 所有操作完成後，一次性儲存
await _userRepository.AddAsync(user);
await _orderRepository.AddAsync(order);
await _dbContext.SaveChangesAsync();  // 只呼叫一次

// ❌ 多次呼叫（無法整體 Rollback）
await _dbContext.SaveChangesAsync();
// ...
await _dbContext.SaveChangesAsync();
```

**Repository 不應呼叫 SaveChangesAsync，只有外層呼叫。**

## Migration 工作流程

```bash
# 新增 Migration（startup-project 必用 src/AiTeam.Dashboard / 含 EF Core Design / 多 DbContext 必加 --context）
dotnet ef migrations add [MigrationName] --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext

# 套用到本機資料庫（Aspire 啟動後執行）
dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext

# 確認即將執行的 SQL
dotnet ef migrations script --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
```

**命名慣例：** `Add{Entity}Table`、`Add{Column}To{Table}`、`Update{Table}{描述}`

> 每個 Stage 的 Migration 應在 PR 說明中明確列出，並在驗收前確認已 `database update`。

### ⚠️ Migration AddColumn 必檢視 `defaultValue` 對齊 entity C# property initializer（Stage 76 揭）

EF Core auto-generated Migration **不識別 C# property initializer** — 對 `public int X { get; set; } = 3;` 這類 entity 屬性，自動生成的 Migration `AddColumn<int>` 只會給 `defaultValue: 0`（C# 型別 default），不會把 `= 3` initializer 帶進 DB column default。

**結果**：production apply Migration 後，**既有 row** 該欄位被填成 `0`（不是 entity C# 寫的 `3`）。未來業務邏輯如 `if (AttemptCount < MaxAttempts)` 判斷會永遠 false（MaxAttempts=0）→ 整個 retry path 失效。

**紀律**：

1. 每次 `dotnet ef migrations add` 後**檢視產生的 Migration `.cs` 檔**
2. 每個 `AddColumn<T>` 看 `defaultValue:` 是否對齊 entity C# property initializer
3. 不一致 → **手動 patch Migration `defaultValue`**（不是改 entity / 因為 entity initializer 對「**新建立**的 row」紀律正確 / 只是對「既有 row backfill」DB layer 要補）
4. 加註解標明對齊原因（避免後續維護者誤改）

**典型場景**：

| Entity 屬性 | EF auto-generated | 必手動 patch |
|---|---|---|
| `public int MaxAttempts { get; set; } = 3;` | `defaultValue: 0` | `defaultValue: 3` |
| `public bool IsActive { get; set; } = true;` | `defaultValue: false` | `defaultValue: true` |
| `public string Status { get; set; } = "pending";` | `defaultValue: ""` | `defaultValue: "pending"` |
| nullable 欄（`int?`、`DateTime?`） | `nullable: true` ✓ | 不需 patch（NULL OK） |

**Stage 76 真實案例**：[`20260517164001_Stage76RetrySchema.cs:32`](../../src/AiTeam.Data/Migrations/20260517164001_Stage76RetrySchema.cs#L32) `MaxAttempts defaultValue 0 → 3` 手動 patch。

**對齊既有紀律**：Trial_v9 + Stage 67 揭「PostgreSQL NULL unique 雷三層修根因」同類根因延伸 — Migration 不全照 entity C# 行為 / 必檢視。

## Repository 模式

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
}
```

## 提交前檢查

- [ ] 使用 Include 避免 N+1
- [ ] LINQ 在伺服器端執行
- [ ] 所有資料庫操作使用 Async 方法
- [ ] 只讀查詢使用 AsNoTracking
- [ ] catch 區塊捕捉 `PostgresException`（而非 `SqlException`），且有 `ChangeTracker.Clear()`
- [ ] SaveChangesAsync 只在最外層呼叫一次
- [ ] 有新 Entity 或欄位變更時，已新增對應 Migration 並確認可套用
