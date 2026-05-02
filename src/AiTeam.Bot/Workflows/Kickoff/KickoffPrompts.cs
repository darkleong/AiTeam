using System.Text;
using System.Text.Json;

namespace AiTeam.Bot.Workflows.Kickoff;

/// <summary>
/// Stage 50：Kick-off 會議 Agent prompt builders + Petra decision parser。
/// 從 legacy KickoffMeetingService 抽出共用（Aria 計劃書範圍擴張認可，純機械化重構，prompt 文字 0 變動）。
///
/// SoT 對齊：legacy KickoffMeetingService 6 個 prompt builders + TryParsePetraDecision 改委派此檔，
/// feature flag 兩條路徑（legacy + framework）共用同 prompt SoT，避免漂移。
/// </summary>
internal static class KickoffPrompts
{
    public static string BuildRosaPrompt(
        string proposal, int round, string? previousPetraOutput, string? midInterruptHint = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Rosa，負責需求分析的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        AppendMidInterruptHint(sb, midInterruptHint);
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的需求分析意見。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從需求分析師角度，評估此需求的完整性。");
            sb.AppendLine("請指出：有哪些模糊之處？有哪些矛盾？缺少什麼關鍵資訊？");
            sb.AppendLine("你可以讀取 codebase 中的相關檔案來了解現有設計。");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的意見，不需要執行任何實作工作。");
        return sb.ToString();
    }

    public static string BuildDemiPrompt(
        string proposal, int round, string? previousPetraOutput, string? midInterruptHint = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Demi，負責 UI/UX 設計的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        AppendMidInterruptHint(sb, midInterruptHint);
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的 UI/UX 評估意見。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從 UI/UX 設計師角度，評估此需求對現有 Dashboard 的影響。");
            sb.AppendLine("請指出：會影響哪些現有頁面或元件？有哪些設計疑慮？");
            sb.AppendLine("你可以讀取 Dashboard 相關的 Blazor 元件檔案來了解現有設計。");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的意見，不需要執行任何實作工作。");
        return sb.ToString();
    }

    public static string BuildCodyPrompt(
        string proposal, int round, string? previousPetraOutput, string? midInterruptHint = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Cody，負責後端開發的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        AppendMidInterruptHint(sb, midInterruptHint);
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的技術可行性評估。如需讀取 code 確認，請直接讀取。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從開發者角度，評估此需求的技術可行性。");
            sb.AppendLine("請讀取相關 codebase 確認現有架構是否支援此功能，指出技術風險與實作難點。");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作（不要寫程式碼）。");
        return sb.ToString();
    }

    public static string BuildQuinnPrompt(
        string proposal, int round, string? previousPetraOutput, string? midInterruptHint = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Quinn，負責 QA 測試的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        AppendMidInterruptHint(sb, midInterruptHint);
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的測試可行性評估。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從 QA 角度，評估此需求的可測試性。");
            sb.AppendLine("請指出：哪些部分難以自動化測試？需要什麼測試策略？有什麼潛在的測試盲點？");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作。");
        return sb.ToString();
    }

    public static string BuildPetraRoundPrompt(
        string rosaOutput, string demiOutput, string codyOutput, string quinnOutput, int round,
        string? midInterruptHint = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Petra，AI 團隊的 PM，正在主持 Kick-off 會議。");
        sb.AppendLine();
        AppendMidInterruptHint(sb, midInterruptHint);
        sb.AppendLine($"## 第 {round} 輪各角色意見");
        sb.AppendLine();
        sb.AppendLine("### Rosa（需求分析）");
        sb.AppendLine(rosaOutput);
        sb.AppendLine();
        sb.AppendLine("### Demi（UI/UX 設計）");
        sb.AppendLine(demiOutput);
        sb.AppendLine();
        sb.AppendLine("### Cody（技術可行性）");
        sb.AppendLine(codyOutput);
        sb.AppendLine();
        sb.AppendLine("### Quinn（測試規劃）");
        sb.AppendLine(quinnOutput);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("整理以上所有意見，判斷是否有需要進一步討論的重大分歧。");
        sb.AppendLine("你可以讀取 codebase 確認技術細節的準確性。");
        sb.AppendLine();
        sb.AppendLine("在回應最後，輸出以下 JSON（單獨一行，不要包在 code block 中）：");
        sb.AppendLine("{\"decision\":\"consensus|needs_discussion|escalate\",\"summary\":\"整理摘要\",\"discussion_points\":[\"需要進一步討論的點\"]}");
        sb.AppendLine();
        sb.AppendLine("decision 說明：");
        sb.AppendLine("- consensus：沒有重大分歧，可以繼續");
        sb.AppendLine("- needs_discussion：有需要討論的分歧，進行下一輪");
        sb.AppendLine("- escalate：有無法在團隊內解決的問題，需要老闆決定");
        return sb.ToString();
    }

    public static string BuildPetraPlanPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Kick-off 會議已結束。請基於完整的會議討論，產出任務計劃書。");
        sb.AppendLine();
        sb.AppendLine("格式如下：");
        sb.AppendLine("# 任務計劃書");
        sb.AppendLine("## 任務摘要");
        sb.AppendLine("{一段話描述要做什麼}");
        sb.AppendLine("## 關鍵決策");
        sb.AppendLine("- {Kick-off 中達成的共識}");
        sb.AppendLine("## 各角色意見摘要");
        sb.AppendLine("| 角色 | 主要意見 | 結論 |");
        sb.AppendLine("|------|---------|------|");
        sb.AppendLine("| Rosa | ... | 已確認 / 待 Christ 決定 |");
        sb.AppendLine("## 風險與注意事項");
        sb.AppendLine("- {Kick-off 中提出但未完全解決的項目}");
        sb.AppendLine("## 建議實作方向");
        sb.AppendLine("{基於討論結果的技術方向建議}");
        return sb.ToString();
    }

    /// <summary>
    /// Stage 51：HITL 中途介入指引 prompt 注入 helper。
    /// midInterruptHint 來自 KickoffState.MidInterruptResponse（Christ apply 時的修改指引文字）。
    /// 拍板「持續保留」（每輪都注入），對齊 ModifyTaskPlanAsync「Petra 永遠記得」精神；
    /// Cancel 時 caller 應傳 null（已在 MidInterruptCheckExecutor.HandleResponseAsync 處理）。
    /// </summary>
    public static void AppendMidInterruptHint(StringBuilder sb, string? midInterruptHint)
    {
        if (string.IsNullOrWhiteSpace(midInterruptHint)) return;
        sb.AppendLine("## ⚠️ 老闆中途介入指引（必須優先考量）");
        sb.AppendLine(midInterruptHint);
        sb.AppendLine();
    }

    /// <summary>
    /// 從 Petra 回應的最後幾行尋找 decision JSON。
    /// 對齊 legacy KickoffMeetingService.TryParsePetraDecision 邏輯。
    /// </summary>
    public static PetraDecision? TryParsePetraDecision(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<PetraDecision>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    /// <summary>
    /// 從 Petra 回應的最後幾行尋找 modify decision JSON。
    /// 對齊 legacy KickoffMeetingService.TryParseModifyDecision 邏輯。
    /// </summary>
    public static ModifyDecision? TryParseModifyDecision(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<ModifyDecision>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }
}

/// <summary>Petra Kick-off round 整理判斷（對齊 legacy KickoffMeetingService 內 internal record）。</summary>
internal record PetraDecision(
    string   Decision,
    string   Summary,
    string[] DiscussionPoints);

/// <summary>Christ 修改 TaskPlan 後 Petra 的判斷回應。</summary>
internal record ModifyDecision(
    string Impact,
    string RevisedPlan);
