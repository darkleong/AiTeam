我需要查看更多現有程式碼才能提供完整實作。以下是基於任務描述的完整實作：

src/AiTeam.Data/Models/TaskItem.cs

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiTeam.Data.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Todo;

        [Required]
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public int? AssigneeId { get; set; }

        [ForeignKey(nameof(AssigneeId))]
        public virtual AppUser? Assignee { get; set; }

        public int? ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? Tags { get; set; }
    }

    public enum TaskStatus
    {
        Todo,
        InProgress,
        Review,
        Done,
        Cancelled
    }

    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}

src/AiTeam.Shared/Services/ICsvExportService.cs

using System.Collections.Generic;
using AiTeam.Data.Models;

namespace AiTeam.Shared.Services
{
    public interface ICsvExportService
    {
        byte[] ExportTasksToCsv(IEnumerable<TaskItem> tasks);
        string GetTasksCsvFileName();
    }
}

src/AiTeam.Shared/Services/CsvExportService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiTeam.Data.Models;

namespace AiTeam.Shared.Services
{
    public class CsvExportService : ICsvExportService
    {
        private static readonly string[] Headers = new[]
        {
            "編號",
            "標題",
            "描述",
            "狀態",
            "優先級",
            "負責人",
            "專案",
            "截止日期",
            "標籤",
            "建立時間",
            "更新時間"
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
                    task.Title,
                    task.Description ?? string.Empty,
                    TranslateStatus(task.Status),
                    TranslatePriority(task.Priority),
                    task.Assignee?.UserName ?? string.Empty,
                    task.Project?.Name ?? string.Empty,
                    task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy/MM/dd") : string.Empty,
                    task.Tags ?? string.Empty,
                    task.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"),
                    task.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss")
                };

                sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
            }

            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return utf8WithBom.GetBytes(sb.ToString());
        }

        public string GetTasksCsvFileName()
        {
            return $"tasks_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private static string TranslateStatus(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Todo => "待處理",
                TaskStatus.InProgress => "進行中",
                TaskStatus.Review => "審核中",
                TaskStatus.Done => "已完成",
                TaskStatus.Cancelled => "已取消",
                _ => status.ToString()
            };
        }

        private static string TranslatePriority(TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.Low => "低",
                TaskPriority.Medium => "中",
                TaskPriority.High => "高",
                TaskPriority.Critical => "緊急",
                _ => priority.ToString()
            };
        }
    }
}

src/AiTeam.Web/Pages/Tasks.razor

@page "/tasks"
@using AiTeam.Data.Models
@using AiTeam.Shared.Services
@inject ICsvExportService CsvExportService
@inject IJSRuntime JSRuntime

<PageTitle>任務列表</PageTitle>

<div class="container-fluid">
    <div class="row mb-3">
        <div class="col">
            <h1 class="h3">任務列表</h1>
        </div>
        <div class="col-auto d-flex align-items-center gap-2">
            <button class="btn btn-outline-success" @onclick="ExportCsvAsync" disabled="@_isExporting">
                @if (_isExporting)
                {
                    <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                }
                else
                {
                    <i class="bi bi-download me-1"></i>
                }
                匯出 CSV
            </button>
            <a href="/tasks/create" class="btn btn-primary">
                <i class="bi bi-plus-circle me-1"></i>新增任務
            </a>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <div class="row g-3">
                <div class="col-md-4">
                    <label class="form-label">搜尋</label>
                    <input type="text" class="form-control" placeholder="搜尋標題或描述..."
                           @bind="_filterKeyword" @bind:event="oninput" @onchange="OnFilterChanged" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">狀態</label>
                    <select class="form-select" @bind="_filterStatus" @bind:after="OnFilterChanged">
                        <option value="">全部</option>
                        <option value="Todo">待處理</option>
                        <option value="InProgress">進行中</option>
                        <option value="Review">審核中</option>
                        <option value="Done">已完成</option>
                        <option value="Cancelled">已取消</option>
                    </select>
                </div>
                <div class="col-md-2">
                    <label class="form-label">優先級</label>
                    <select class="form-select" @bind="_filterPriority" @bind:after="OnFilterChanged">
                        <option value="">全部</option>
                        <option value="Low">低</option>
                        <option value="Medium">中</option>
                        <option value="High">高</option>
                        <option value="Critical">緊急</option>
                    </select>
                </div>
                <div class="col-md-2">
                    <label class="form-label">截止日期（起）</label>
                    <input type="date" class="form-control" @bind="_filterDueDateFrom" @bind:after="OnFilterChanged" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">截止日期（迄）</label>
                    <input type="date" class="form-control" @bind="_filterDueDateTo" @bind:after="OnFilterChanged" />
                </div>
            </div>
        </div>
    </div>

    @if (_isLoading)
    {
        <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">載入中...</span>
            </div>
        </div>
    }
    else if (!_filteredTasks.Any())
    {
        <div class="text-center py-5 text-muted">
            <i class="bi bi-inbox fs-1 d-block mb-2"></i>
            目前沒有符合條件的任務
        </div>
    }
    else
    {
        <div class="card">
            <div class="table-responsive">
                <table class="table table-hover mb-0">
                    <thead class="table-light">
                        <tr>
                            <th>編號</th>
                            <th>標題</th>
                            <th>狀態</th>
                            <th>優先級</th>
                            <th>負責人</th>
                            <th>專案</th>
                            <th>截止日期</th>
                            <th>操作</th>
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
                                        @GetStatusDisplayName(task.Status)
                                    </span>
                                </td>
                                <td>
                                    <span class="badge @GetPriorityBadgeClass(task.Priority)">
                                        @GetPriorityDisplayName(task.Priority)
                                    </span>
                                </td>
                                <td>@(task.Assignee?.UserName ?? "-")</td>
                                <td>@(task.Project?.Name ?? "-")</td>
                                <td>@(task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy/MM/dd") : "-")</td>
                                <td>
                                    <a href="/tasks/@task.Id" class="btn btn-sm btn-outline-primary me-1">檢視</a>
                                    <a href="/tasks/@task.Id/edit" class="btn btn-sm btn-outline-secondary">編輯</a>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </div>

        <div class="mt-2 text-muted small">
            共 @_filteredTasks.Count() 筆任務
        </div>
    }
</div>

src/AiTeam.Web/Pages/Tasks.razor.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiTeam.Data.Models;
using AiTeam.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TaskStatus = AiTeam.Data.Models.TaskStatus;

namespace AiTeam.Web.Pages
{
    public partial class Tasks : ComponentBase
    {
        [Inject] private ICsvExportService CsvExportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private ITaskService TaskService { get; set; } = default!;

        private List<TaskItem> _allTasks = new();
        private IEnumerable<TaskItem> _filteredTasks = Enumerable.Empty<TaskItem>();
        private bool _isLoading = true;
        private bool _isExporting = false;

        private string _filterKeyword = string.Empty;
        private string _filterStatus = string.Empty;
        private string _filterPriority = string.Empty;
        private DateTime? _filterDueDateFrom;
        private DateTime? _filterDueDateTo;

        protected override async Task OnInitializedAsync()
        {
            await LoadTasksAsync();
        }

        private async Task LoadTasksAsync()
        {
            _isLoading = true;
            try
            {
                _allTasks = (await TaskService.GetAllTasksAsync()).ToList();
                ApplyFilters();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnFilterChanged()
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            IEnumerable<TaskItem> query = _allTasks;

            if (!string.IsNullOrWhiteSpace(_filterKeyword))
            {
                var keyword = _filterKeyword.Trim().ToLowerInvariant();
                query = query.Where(t =>
                    t.Title.ToLowerInvariant().Contains(keyword) ||
                    (t.Description != null && t.Description.ToLowerInvariant().Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(_filterStatus) &&
                Enum.TryParse<TaskStatus>(_filterStatus, out var statusEnum))
            {
                query = query.Where(t => t.Status == statusEnum);
            }

            if (!string.IsNullOrWhiteSpace(_filterPriority) &&
                Enum.TryParse<TaskPriority>(_filterPriority, out var priorityEnum))
            {
                query = query.Where(t => t.Priority == priorityEnum);
            }

            if (_filterDueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date >= _filterDueDateFrom.Value.Date);
            }

            if (_filterDueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date <= _filterDueDateTo.Value.Date);
            }

            _filteredTasks = query.ToList();
        }

        private async Task ExportCsvAsync()
        {
            if (_isExporting) return;

            _isExporting = true;
            try
            {
                var csvBytes = CsvExportService.ExportTasksToCsv(_filteredTasks);
                var fileName = CsvExportService.GetTasksCsvFileName();
                var base64 = Convert.ToBase64String(csvBytes);

                await JSRuntime.InvokeVoidAsync(
                    "downloadFileFromBase64",
                    fileName,
                    "text/csv;charset=utf-8",
                    base64);
            }
            finally
            {
                _isExporting = false;
            }
        }

        private static string GetStatusDisplayName(TaskStatus status) => status switch
        {
            TaskStatus.Todo => "待處理",
            TaskStatus.InProgress => "進行中",
            TaskStatus.Review => "審核中",
            TaskStatus.Done => "已完成",
            TaskStatus.Cancelled =>