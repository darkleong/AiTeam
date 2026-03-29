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

```csharp
catch (DbUpdateException ex) when (ex.InnerException is SqlException sx)
{
    _dbContext.ChangeTracker.Clear();  // 必須清除追蹤狀態
    return sx.Number switch
    {
        2627 => BadRequest("Unique constraint error."),
        547  => BadRequest("Constraint check violation."),
        2601 => BadRequest("Duplicated key row error."),
        _    => BadRequest(ex.Message),
    };
}
catch (Exception ex)
{
    _dbContext.ChangeTracker.Clear();  // 必須清除追蹤狀態
    return BadRequest(ex.ToString());
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
- [ ] catch 區塊有 ChangeTracker.Clear()
- [ ] SaveChangesAsync 只在最外層呼叫一次
