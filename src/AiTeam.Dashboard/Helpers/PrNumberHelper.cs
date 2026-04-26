namespace AiTeam.Dashboard.Helpers;

public static class PrNumberHelper
{
    public static string ExtractPrNumber(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "PR";
        var last = url.TrimEnd('/').Split('/')[^1];
        return int.TryParse(last, out var num) ? $"#{num}" : "PR";
    }
}
