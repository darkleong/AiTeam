
```csharp
using System.Text;

namespace AiTeam.Shared.Services;

public interface ICsvExportService
{
    byte[] ExportToCsv<T>(IEnumerable<T> data, IReadOnlyDictionary<string, Func<T, string>> columnDefinitions);
    byte[] ExportTasksToCsv(IEnumerable<TaskCsvRow> tasks);
}

public record TaskCsvRow(
    string Id,
    string Title,
    string Status,
    string Priority,
    string AssignedTo,
    string CreatedBy,
    string CreatedAt,
    string UpdatedAt,
    string DueDate,
    string Tags,
    string Description
);

public class CsvExportService : ICsvExportService
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    public byte[] ExportToCsv<T>(IEnumerable<T> data, IReadOnlyDictionary<string, Func<T, string>> columnDefinitions)
    {
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join(",", columnDefinitions.Keys.Select(EscapeCsvField)));

        // Data rows
        foreach (var item in data)
        {
            var fields = columnDefinitions.Values.Select(getValue => EscapeCsvField(getValue(item)));
            sb.AppendLine(string.Join(",", fields));
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[Utf8Bom.Length + csvBytes.Length];
        Utf8Bom.CopyTo(result, 0);
        csvBytes.CopyTo(result, Utf8Bom.Length);

        return result;
    }

    public byte[] ExportTasksToCsv(IEnumerable<TaskCsvRow> tasks)
    {
        var columns = new Dictionary<string, Func<TaskCsvRow, string>>
        {
            ["編號"] = t => t.Id,
            ["標題"] = t => t.Title,
            ["狀態"] = t => t.Status,
            ["優先級"] = t => t.Priority,
            ["負責人"] = t => t.AssignedTo,
            ["建立者"] = t => t.CreatedBy,
            ["建立時間"] = t => t.CreatedAt,
            ["更新時間"] = t => t.UpdatedAt,
            ["截止日期"] = t => t.DueDate,
            ["標籤"] = t => t.Tags,
            ["描述"] = t => t.Description
        };

        return ExportToCsv(tasks, columns);
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
```