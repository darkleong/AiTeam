我需要先了解現有的程式碼結構才能實作完整功能。由於現有程式碼為空，我將根據任務描述實作完整的 CSV 匯出功能。

```csharp
// src/AiTeam.Shared/Services/ICsvExportService.cs
using AiTeam.Shared.Models;

namespace AiTeam.Shared.Services;

public interface ICsvExportService
{
    byte[] ExportTasksToCsv(IEnumerable<TaskItem> tasks);
}
```

```csharp
// src/AiTeam.Shared/Services/CsvExportService.cs
using System.Text;
using AiTeam.Shared.Models;

namespace AiTeam.Shared.Services;

public class CsvExportService : ICsvExportService
{
    private static readonly string[] Headers = new[]
    {
        "任務編號", "任務名稱", "狀態", "優先級", "負責人", "專案", "建立日期", "截止日期", "完成日期", "描述"
    };

    public byte[] ExportTasksToCsv(IEnumerable<TaskItem> tasks)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(",", Headers.Select(EscapeCsvField)));

        foreach (var task in tasks)
        {
            var row = new[]
            {
                task.Id.ToString(),
                task.Title ?? string.Empty,
                task.Status ?? string.Empty,
                task.Priority ?? string.Empty,
                task.Assignee ?? string.Empty,
                task.Project ?? string.Empty,
                task.CreatedAt.ToString("yyyy-MM-dd"),
                task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                task.CompletedAt.HasValue ? task.CompletedAt.Value.ToString("yyyy-MM-dd") : string.Empty,
                task.Description ?? string.Empty
            };

            sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
        }

        var bomBytes = Encoding.UTF8.GetPreamble();
        var contentBytes = Encoding.UTF8.GetBytes(sb.ToString());

        var result = new byte[bomBytes.Length + contentBytes.Length];
        Buffer.BlockCopy(bomBytes, 0, result, 0, bomBytes.Length);
        Buffer.BlockCopy(contentBytes, 0, result, bomBytes.Length, contentBytes.Length);

        return result;
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
```

```csharp
// src/AiTeam.Shared/Models/TaskItem.cs
namespace AiTeam.Shared.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Assignee { get; set; }
    public string? Project { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Description { get; set; }
}
```

```csharp
// src/AiTeam.Dashboard/Pages/Tasks.razor.cs
using AiTeam.Shared.Models;
using AiTeam.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AiTeam.Dashboard.Pages;

public partial class Tasks : ComponentBase
{
    [Inject] private ICsvExportService CsvExportService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ITaskService TaskService { get; set; } = default!;

    private List<TaskItem> _allTasks = new();
    private List<TaskItem> _filteredTasks = new();
    private bool _isLoading = false;
    private bool _isExporting = false;

    // 篩選條件
    private string? _filterStatus;
    private string? _filterPriority;
    private string? _filterAssignee;
    private string? _filterProject;
    private string? _searchText;
    private DateTime? _filterDueDateFrom;
    private DateTime? _filterDueDateTo;

    protected override async Task OnInitializedAsync()
    {
        await LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            _allTasks = await TaskService.GetTasksAsync();
            ApplyFilters();
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void ApplyFilters()
    {
        var query = _allTasks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(t =>
                (t.Title?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Description?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(_filterStatus))
        {
            query = query.Where(t => t.Status == _filterStatus);
        }

        if (!string.IsNullOrWhiteSpace(_filterPriority))
        {
            query = query.Where(t => t.Priority == _filterPriority);
        }

        if (!string.IsNullOrWhiteSpace(_filterAssignee))
        {
            query = query.Where(t => t.Assignee == _filterAssignee);
        }

        if (!string.IsNullOrWhiteSpace(_filterProject))
        {
            query = query.Where(t => t.Project == _filterProject);
        }

        if (_filterDueDateFrom.HasValue)
        {
            query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value >= _filterDueDateFrom.Value);
        }

        if (_filterDueDateTo.HasValue)
        {
            query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value <= _filterDueDateTo.Value);
        }

        _filteredTasks = query.ToList();
        StateHasChanged();
    }

    private void OnSearchTextChanged(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString();
        ApplyFilters();
    }

    private void OnStatusFilterChanged(ChangeEventArgs e)
    {
        _filterStatus = e.Value?.ToString();
        ApplyFilters();
    }

    private void OnPriorityFilterChanged(ChangeEventArgs e)
    {
        _filterPriority = e.Value?.ToString();
        ApplyFilters();
    }

    private void OnAssigneeFilterChanged(ChangeEventArgs e)
    {
        _filterAssignee = e.Value?.ToString();
        ApplyFilters();
    }

    private void OnProjectFilterChanged(ChangeEventArgs e)
    {
        _filterProject = e.Value?.ToString();
        ApplyFilters();
    }

    private void ClearFilters()
    {
        _filterStatus = null;
        _filterPriority = null;
        _filterAssignee = null;
        _filterProject = null;
        _searchText = null;
        _filterDueDateFrom = null;
        _filterDueDateTo = null;
        ApplyFilters();
    }

    private async Task ExportToCsvAsync()
    {
        if (_isExporting) return;

        _isExporting = true;
        StateHasChanged();

        try
        {
            var csvBytes = CsvExportService.ExportTasksToCsv(_filteredTasks);
            var fileName = $"tasks_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            await DownloadFileAsync(fileName, csvBytes, "text/csv");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CSV 匯出失敗: {ex.Message}");
        }
        finally
        {
            _isExporting = false;
            StateHasChanged();
        }
    }

    private async Task DownloadFileAsync(string fileName, byte[] content, string mimeType)
    {
        var base64 = Convert.ToBase64String(content);
        await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, mimeType);
    }
}
```

```razor
@* src/AiTeam.Dashboard/Pages/Tasks.razor *@
@page "/tasks"
@using AiTeam.Shared.Models

<PageTitle>任務列表</PageTitle>

<div class="container-fluid px-4">
    <div class="d-flex justify-content-between align-items-center mb-4">
        <h1 class="h3 mb-0">任務列表</h1>
        <button class="btn btn-success"
                @onclick="ExportToCsvAsync"
                disabled="@(_isExporting || _isLoading)">
            @if (_isExporting)
            {
                <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                <span>匯出中...</span>
            }
            else
            {
                <i class="bi bi-download me-1"></i>
                <span>匯出 CSV</span>
            }
        </button>
    </div>

    <div class="card mb-4">
        <div class="card-header">
            <i class="bi bi-funnel me-1"></i>篩選條件
        </div>
        <div class="card-body">
            <div class="row g-3">
                <div class="col-md-4">
                    <label class="form-label">搜尋</label>
                    <input type="text"
                           class="form-control"
                           placeholder="搜尋任務名稱或描述..."
                           value="@_searchText"
                           @oninput="OnSearchTextChanged" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">狀態</label>
                    <select class="form-select" @onchange="OnStatusFilterChanged">
                        <option value="">全部</option>
                        <option value="待處理">待處理</option>
                        <option value="進行中">進行中</option>
                        <option value="已完成">已完成</option>
                        <option value="已取消">已取消</option>
                    </select>
                </div>
                <div class="col-md-2">
                    <label class="form-label">優先級</label>
                    <select class="form-select" @onchange="OnPriorityFilterChanged">
                        <option value="">全部</option>
                        <option value="高">高</option>
                        <option value="中">中</option>
                        <option value="低">低</option>
                    </select>
                </div>
                <div class="col-md-2">
                    <label class="form-label">負責人</label>
                    <input type="text"
                           class="form-control"
                           placeholder="負責人..."
                           @onchange="OnAssigneeFilterChanged" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">專案</label>
                    <input type="text"
                           class="form-control"
                           placeholder="專案名稱..."
                           @onchange="OnProjectFilterChanged" />
                </div>
            </div>
            <div class="mt-3">
                <button class="btn btn-outline-secondary btn-sm" @onclick="ClearFilters">
                    <i class="bi bi-x-circle me-1"></i>清除篩選
                </button>
                <span class="ms-3 text-muted small">
                    共 @_filteredTasks.Count 筆任務
                    @if (_filteredTasks.Count != _allTasks.Count)
                    {
                        <span>（篩選自 @_allTasks.Count 筆）</span>
                    }
                </span>
            </div>
        </div>
    </div>

    @if (_isLoading)
    {
        <div class="d-flex justify-content-center p-5">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">載入中...</span>
            </div>
        </div>
    }
    else if (!_filteredTasks.Any())
    {
        <div class="alert alert-info">
            <i class="bi bi-info-circle me-2"></i>沒有符合條件的任務。
        </div>
    }
    else
    {
        <div class="card">
            <div class="card-body p-0">
                <div class="table-responsive">
                    <table class="table table-hover mb-0">
                        <thead class="table-light">
                            <tr>
                                <th>任務編號</th>
                                <th>任務名稱</th>
                                <th>狀態</th>
                                <th>優先級</th>
                                <th>負責人</th>
                                <th>專案</th>
                                <th>建立日期</th>
                                <th>截止日期</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var task in _filteredTasks)
                            {
                                <tr>
                                    <td>@task.Id</td>
                                    <td>@task.Title</td>
                                    <td>
                                        <span class="badge @GetStatusBadgeClass(task.Status)">
                                            @task.Status
                                        </span>
                                    </td>
                                    <td>
                                        <span class="badge @GetPriorityBadgeClass(task.Priority)">
                                            @task.Priority
                                        </span>
                                    </td>
                                    <td>@task.Assignee</td>
                                    <td>@task.Project</td>
                                    <td>@task.CreatedAt.ToString("yyyy-MM-dd")</td>
                                    <td>@(task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : "-")</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    }
</div>

@code {
    private static string GetStatusBadgeClass(string? status) => status switch
    {
        "進行中" => "bg-primary",
        "已完成" => "bg-success",
        "待處理" => "bg-warning text-dark",
        "已取消" => "bg-secondary",
        _ => "bg-light text-dark"
    };

    private static string GetPriorityBadgeClass(string? priority) => priority switch
    {
        "高" => "bg-danger",
        "中" => "bg-warning text-dark",
        "低" => "bg-info text-dark",
        _ => "