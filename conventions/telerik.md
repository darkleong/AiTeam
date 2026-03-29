# Telerik 組件規範

## TelerikGrid 標準設定

```razor
<TelerikGrid Data="@GridData"
             @ref="@GridRef"
             Resizable="true"
             Reorderable="true"
             Sortable="true"
             SortMode="@SortMode.Multiple"
             Pageable="true"
             PageSize="50"
             Height="600px"
             Class="table"
             SelectionMode="@GridSelectionMode.Multiple"
             SelectedItemsChanged="@OnSelectionChangedAsync"
             OnRead="@OnGridReadAsync">

    <GridToolBarTemplate>
        <GridSearchBox />
        <TelerikButton ThemeColor="primary" OnClick="@CreateNewAsync">
            Add New
        </TelerikButton>
    </GridToolBarTemplate>

    <GridColumns>
        <GridCheckboxColumn SelectAll="true" Width="50px" />
        <GridColumn Field="@nameof(UserDto.Id)" Title="ID" Width="80px" Locked="true" Filterable="true" />
        <GridColumn Field="@nameof(UserDto.Name)" Title="Name" Filterable="true" Sortable="true" />
        <GridColumn Field="@nameof(UserDto.CreatedDate)"
                    Title="Created"
                    Format="{0:yyyy-MM-dd}"
                    Sortable="true" />
        <GridColumn Title="Actions" Width="150px" Locked="true">
            <Template>
                @{
                    var item = context as UserDto;
                    <TelerikButton Size="@ThemeConstants.Button.Size.Small"
                                   ThemeColor="info"
                                   OnClick="@(async () => await EditAsync(item))"
                                   Icon="@SvgIcon.Pencil" />
                    <TelerikButton Size="@ThemeConstants.Button.Size.Small"
                                   ThemeColor="danger"
                                   OnClick="@(async () => await DeleteAsync(item))"
                                   Icon="@SvgIcon.Trash" />
                }
            </Template>
        </GridColumn>
    </GridColumns>
</TelerikGrid>
```

## Grid 推薦參數

| 參數 | 推薦值 | 說明 |
|------|--------|------|
| Resizable | true | 可調整列寬 |
| Reorderable | true | 可拖動重排 |
| Sortable | true | 可排序 |
| SortMode | Multiple | 多欄排序 |
| Pageable | true | 分頁 |
| PageSize | 50 | 每頁筆數 |
| Height | "600px" | 固定高度 |
| @ref | GridRef | 手動控制必要 |

## OnRead 伺服器端分頁（推薦）

```csharp
private async Task OnGridReadAsync(GridReadEventArgs args)
{
    int pageNumber = args.Request.Page;
    int pageSize = args.Request.PageSize;

    var sorts = args.Request.Sorts;
    string? sortBy = sorts?.FirstOrDefault()?.Member;
    bool sortAscending = sorts?.FirstOrDefault()?.SortDirection == ListSortDirection.Ascending;

    var result = await _userService.GetUsersPaginatedAsync(
        pageNumber: pageNumber,
        pageSize: pageSize,
        sortBy: sortBy,
        sortAscending: sortAscending);

    args.Data = result.Items;
    args.Total = result.TotalCount;
}
```

## 手動重新綁定

```csharp
// 刷新整個 Grid
await GridRef!.Rebind();

// 刷新回第一頁
GridRef!.PageIndex = 0;
await GridRef.Rebind();
```

## 自訂 Template

```razor
<GridColumn Field="@nameof(UserDto.IsActive)" Title="Status">
    <Template>
        @{
            var item = context as UserDto;
            var cssClass = item?.IsActive == true ? "badge-success" : "badge-secondary";
            <span class="badge @cssClass">
                @(item?.IsActive == true ? "Active" : "Inactive")
            </span>
        }
    </Template>
</GridColumn>
```

## DetailTemplate（展開行詳情）

```razor
<TelerikGrid Data="@GridData" DetailTemplate="@DetailTemplate">
    <GridColumns>
        <GridColumn Field="@nameof(UserDto.Id)" Title="ID" />
        <GridColumn Field="@nameof(UserDto.Name)" Title="Name" />
    </GridColumns>
</TelerikGrid>

@code {
    private RenderFragment<UserDto> DetailTemplate => user => @<text>
        <div class="card mt-3 ms-3">
            <div class="card-body">
                <h5>User Details</h5>
                <p><strong>Email:</strong> @user.Email</p>
                <p><strong>Created:</strong> @user.CreatedDate.ToString("yyyy-MM-dd")</p>
            </div>
        </div>
    </text>;
}
```

## Grid 常見事件

```csharp
// 行選擇改變
private async Task OnSelectionChangedAsync(IEnumerable<UserDto> selectedItems)
{
    SelectedItems = selectedItems.ToList();
}

// 行雙擊
private async Task OnRowDoubleClickAsync(GridRowClickEventArgs args)
{
    var item = args.Item as UserDto;
    await EditAsync(item);
}
```

## TelerikWindow 模態對話框

```razor
<TelerikWindow @bind-Visible="@ShowEditDialog"
               Modal="true"
               Title="Edit User"
               Width="600px"
               Centered="true">
    <WindowContent>
        <div class="form-group mb-3">
            <label class="form-label">Name</label>
            <TelerikTextBox @bind-Value="@EditingUser.Name" Class="form-control" />
        </div>
    </WindowContent>
    <WindowActions>
        <WindowAction Name="Save" OnClick="@SaveAsync" />
        <WindowAction Name="Cancel" OnClick="@CancelAsync" />
    </WindowActions>
</TelerikWindow>
```

## TelerikDropDownList 帶搜尋

```razor
<TelerikDropDownList Data="@UserList"
                     @bind-Value="@SelectedUserId"
                     ValueField="@nameof(UserDto.Id)"
                     TextField="@nameof(UserDto.Name)"
                     Filterable="true"
                     FilterOperator="@StringFilterOperator.Contains"
                     ClearButton="true"
                     Placeholder="Select a user" />
```

## TelerikDatePicker 完整設定

```razor
<TelerikDatePicker @bind-Value="@SelectedDate"
                   Format="yyyy-MM-dd"
                   Min="@MinDate"
                   Max="@MaxDate"
                   Class="form-control"
                   Placeholder="Select a date" />
```

## 性能優化

**虛擬滾動（資料量 > 1000 筆時使用）：**
```razor
<TelerikGrid Data="@GridData"
             Height="600px"
             VirtualizationMode="@GridVirtualizationMode.Vertical"
             PageSize="50" />
```

**避免頻繁 Rebind：** 使用 OnRead 伺服器端處理，不要每次搜尋都直接修改 GridData。

## 常見錯誤

```razor
{{!-- ❌ 沒有 @ref 無法手動控制 Grid --}}
<TelerikGrid Data="@GridData">

{{!-- ✅ 加上 @ref --}}
<TelerikGrid @ref="@GridRef" Data="@GridData">
```

```razor
{{!-- ❌ 模板中放複雜邏輯 --}}
<Template>
    @{ var text = item?.IsActive && item.Orders.Count > 5 ? "Premium" : "Regular"; }
</Template>

{{!-- ✅ 抽成方法 --}}
<Template>
    @{ <span>@GetUserStatus(item)</span> }
</Template>
```

```csharp
// ❌ OnRead 中多次查詢
private async Task OnGridReadAsync(GridReadEventArgs args)
{
    var users = await _userService.GetUsersAsync();
    var orders = await _orderService.GetOrdersAsync();  // 應合併成一次查詢
}
```

## 提交前檢查

- [ ] 使用 @ref 以便手動控制 Grid
- [ ] Grid 設置了推薦參數（Resizable、Sortable 等）
- [ ] 大量資料使用 OnRead 伺服器端分頁
- [ ] 自訂 Template 使用 `context as T` 轉型
- [ ] Template 中無複雜邏輯，抽成方法
- [ ] 修改資料後呼叫 GridRef.Rebind()
- [ ] OnRead 中只做一次優化查詢
- [ ] 資料量大時使用虛擬滾動
