using System.Collections.Generic;
using AiTeam.Shared.Models;

namespace AiTeam.Shared.Services
{
    public interface ICsvExportService
    {
        byte[] ExportTasksToCsv(IEnumerable<TaskItem> tasks);
    }
}