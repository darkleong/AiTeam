using System.Text;
using System.Text.Json;

namespace AiTeam.Bot.Workflows.Design;

/// <summary>
/// Stage 52：Design Meeting Agent prompt builders + decision parsers（v4 漸進遷移第四步）。
/// 從 legacy DesignMeetingService 抽出共用（純機械化重構，prompt 文字 0 變動）。
///
/// SoT 對齊：legacy DesignMeetingService 9 個 prompt builders + 4 個 TryParse* 改委派此檔，
/// feature flag 兩條路徑（legacy + framework）共用同 prompt SoT，避免漂移。
///
/// 設計約束：
///   - 對齊 Stage 50 KickoffPrompts.cs 抽出 SoT 慣例
///   - record DesignPetraDecision / DesignAdjustmentEvaluation 也搬進此檔（注意 Stage 50 踩坑 #2 — 跨 service 用 internal record 必須 grep 全 callers 補 using）
///   - ModifyDecision 沿用 KickoffPrompts.ModifyDecision（不重抽，避免雙寫）
/// </summary>
internal static class DesignPrompts
{
    // ============================================================
    //  前置作業 prompt builders
    // ============================================================

    public static string BuildDesignPetraJudgePrompt(string taskPlan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Petra，AI 團隊的 PM，正在判斷設計階段是否需要 Demi 參與 UI/UX 設計。");
        sb.AppendLine();
        sb.AppendLine("## 任務計劃書");
        sb.AppendLine(taskPlan);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("根據任務計劃書，判斷此功能是否需要 Dashboard UI 設計（例如：新頁面、新元件、Layout 調整等）。");
        sb.AppendLine("你可以讀取現有 Dashboard 相關的 Blazor 元件檔案來輔助判斷。");
        sb.AppendLine();
        sb.AppendLine("請在回應最後輸出以下 JSON（單獨一行，不加 code block）：");
        sb.AppendLine("{\"needs_demi\":true,\"reason\":\"判斷依據\"}");
        sb.AppendLine("- needs_demi: true = 有 Dashboard UI 變更（新頁面/元件/Layout）；false = 純後端/API/DB 調整");
        return sb.ToString();
    }

    public static string BuildDesignRosaPreWorkPrompt(string taskPlan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Rosa，負責需求分析的 AI 團隊成員，正在進行設計前置作業。");
        sb.AppendLine();
        sb.AppendLine("## 任務計劃書（Kick-off 會議產出）");
        sb.AppendLine(taskPlan);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("基於任務計劃書，探索 codebase 並拆解出具體的 GitHub Issues。");
        sb.AppendLine("每個 Issue 代表一個可獨立執行的功能或任務，粒度適中。");
        sb.AppendLine();
        sb.AppendLine("請在回應最後輸出 Issues JSON Array（格式如下，不加 code block）：");
        sb.AppendLine("[{\"title\":\"動詞開頭的具體標題（繁體中文）\",\"body\":\"## 背景\\n...\\n## 驗收條件\\n- [ ] 條件一\",\"labels\":[\"feature\",\"P1\"]}]");
        return sb.ToString();
    }

    public static string BuildDesignDemiPreWorkPrompt(string taskPlan, string issuesJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Demi，負責 UI/UX 設計的 AI 團隊成員，正在進行設計前置作業。");
        sb.AppendLine();
        sb.AppendLine("## 任務計劃書（Kick-off 會議產出）");
        sb.AppendLine(taskPlan);
        sb.AppendLine();
        sb.AppendLine("## Rosa 拆解的 Issues");
        sb.AppendLine(issuesJson);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("基於任務計劃書和 Issues，探索現有 Dashboard（Blazor/MudBlazor）元件後，");
        sb.AppendLine("產出完整的 UI/UX 規格文件（Markdown 格式）。");
        sb.AppendLine("需包含：頁面結構、元件清單、互動說明、MudBlazor 元件建議。");
        return sb.ToString();
    }

    // ============================================================
    //  主迴圈 round prompt builders
    // ============================================================

    public static string BuildDesignRosaMeetingPrompt(string issuesJson, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        if (round == 1)
        {
            sb.AppendLine("設計會議第 1 輪開始。你在前置作業中產出了以下 Issues：");
            sb.AppendLine(issuesJson);
            sb.AppendLine();
            sb.AppendLine("請簡要說明你的需求拆分理由，以及對整體設計方向的想法。");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine($"設計會議第 {round} 輪。Petra 上一輪整理的討論點：");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充或修正你對 Issues 拆分的說明。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接發表意見，不需要執行任何修改。");
        return sb.ToString();
    }

    public static string BuildDesignDemiMeetingPrompt(string uiSpec, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        if (round == 1)
        {
            sb.AppendLine("設計會議第 1 輪開始。你在前置作業中產出了 UI/UX 規格。");
            sb.AppendLine("請簡要說明你的設計決策理由，以及對 UI 設計方向的想法。");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine($"設計會議第 {round} 輪。Petra 上一輪整理的討論點：");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充或修正你對 UI 規格的說明。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接發表意見，不需要執行任何修改。");
        return sb.ToString();
    }

    public static string BuildDesignCodyPrompt(string issuesJson, string? uiSpec, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Cody，負責後端開發，正在參加設計會議。");
        sb.AppendLine();
        if (round == 1)
        {
            sb.AppendLine("## Rosa 拆解的 Issues");
            sb.AppendLine(issuesJson);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(uiSpec))
            {
                sb.AppendLine("## Demi 的 UI/UX 規格");
                sb.AppendLine(uiSpec);
                sb.AppendLine();
            }
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從開發者角度，評估 Issues 拆分的合理性和技術可行性。");
            sb.AppendLine("請讀取相關 codebase 確認現有架構，指出 Issues 間的依賴關係、技術風險、潛在實作困難。");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充你的技術評估意見。如需讀取 code 確認，請直接讀取。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作（不要寫程式碼）。");
        return sb.ToString();
    }

    public static string BuildDesignQuinnPrompt(string issuesJson, string? uiSpec, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Quinn，負責 QA 測試，正在參加設計會議。");
        sb.AppendLine();
        if (round == 1)
        {
            sb.AppendLine("## Rosa 拆解的 Issues");
            sb.AppendLine(issuesJson);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(uiSpec))
            {
                sb.AppendLine("## Demi 的 UI/UX 規格");
                sb.AppendLine(uiSpec);
                sb.AppendLine();
            }
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從 QA 角度，評估 Issues 的可測試性。");
            sb.AppendLine("請指出：哪些 Issues 難以自動化測試？需要什麼測試策略？有什麼潛在的測試盲點？");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充你的測試規劃意見。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作。");
        return sb.ToString();
    }

    public static string BuildDesignPetraRoundPrompt(
        string rosaOutput, string demiOutput, string codyOutput, string quinnOutput,
        int round, bool hasDemi)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"你是 Petra，正在主持設計會議第 {round} 輪。");
        sb.AppendLine();
        sb.AppendLine($"## 第 {round} 輪各角色意見");
        sb.AppendLine();
        sb.AppendLine("### Rosa（需求分析）");
        sb.AppendLine(rosaOutput);
        sb.AppendLine();
        if (hasDemi && !string.IsNullOrEmpty(demiOutput))
        {
            sb.AppendLine("### Demi（UI/UX 設計）");
            sb.AppendLine(demiOutput);
            sb.AppendLine();
        }
        sb.AppendLine("### Cody（技術可行性）");
        sb.AppendLine(codyOutput);
        sb.AppendLine();
        sb.AppendLine("### Quinn（測試規劃）");
        sb.AppendLine(quinnOutput);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("整理以上所有意見，評估設計成果的合理性、可行性、可測試性。");
        sb.AppendLine("你可以讀取 codebase 確認技術細節。");
        sb.AppendLine();
        AppendPetraDisciplineSection(sb);
        sb.AppendLine("在回應最後，輸出以下 JSON（單獨一行，不加 code block）：");
        sb.AppendLine("{\"decision\":\"consensus|needs_discussion|needs_adjustment|escalate\",\"summary\":\"整理摘要\",\"adjustment_targets\":[],\"adjustment_instructions\":{},\"escalate_reason\":\"\"}");
        sb.AppendLine();
        sb.AppendLine("decision 說明：");
        sb.AppendLine("- consensus：純技術 / 內部設計議題自決，無重大問題 → 繼續（不丟老闆，不需要填 adjustment 欄位）");
        sb.AppendLine("- needs_discussion：團隊內可解決的分歧 → 進入下一輪（不需要填 adjustment 欄位）");
        sb.AppendLine("- needs_adjustment：Issues 或 UI 規格需要修改 → 填 adjustment_targets（\"rosa\"/\"demi\"）和 adjustment_instructions（key 為 \"rosa\"/\"demi\"，value 為修改指示）");
        sb.AppendLine("- escalate：對 Christ 看到行為 / 業務邏輯 / spec 有影響無法團隊內解決 → 填 escalate_reason（給推薦答案 + 理由，不只列三選）");
        return sb.ToString();
    }

    public static string BuildDesignPetraPlanPrompt(string taskPlan, string issuesJson, string? uiSpec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("設計會議已結束。請基於完整的討論 context，產出設計規劃書。");
        sb.AppendLine();
        AppendPetraDisciplineSection(sb);
        sb.AppendLine("格式如下（Markdown）：");
        sb.AppendLine("# 設計規劃書");
        sb.AppendLine("## 需求摘要");
        sb.AppendLine("{來自 TaskPlan 的任務摘要}");
        sb.AppendLine("## GitHub Issues 清單");
        sb.AppendLine("| # | Issue | 標題 | 說明 |");
        sb.AppendLine("|---|-------|------|------|");
        sb.AppendLine("## UI/UX 規格摘要（如適用）");
        sb.AppendLine("{Demi 的 UI 規格重點}");
        sb.AppendLine("## 設計決策");
        sb.AppendLine("- {設計會議中達成的共識}");
        sb.AppendLine("## 各角色意見摘要");
        sb.AppendLine("| 角色 | 主要意見 | 結論 |");
        sb.AppendLine("|------|---------|------|");
        sb.AppendLine("## 風險與注意事項");
        sb.AppendLine("- {設計會議中提出但未完全解決的項目}");
        sb.AppendLine("## 開發建議");
        sb.AppendLine("{基於設計審查的技術方向建議}");
        return sb.ToString();
    }

    /// <summary>
    /// Stage 61-FF 五十六：Petra 議題層次紀律 + 給定見紀律 + 工時禁字紀律共用注入段。
    /// SoT 對齊（5 位置維護）：CLAUDE_Petra.md「議題層次紀律 + 給定見紀律 + 工時禁字紀律」段
    /// + KickoffPrompts.BuildPetraRoundPrompt + BuildPetraPlanPrompt
    /// + DesignPrompts.BuildDesignPetraRoundPrompt + BuildDesignPetraPlanPrompt（KickoffPrompts 內各自有同名 helper）。
    /// 修一處要全部對齊（commit message 標 SoT 維護筆記，避免漂移）。
    /// </summary>
    public static void AppendPetraDisciplineSection(StringBuilder sb)
    {
        sb.AppendLine("## 紀律（議題層次 + 給定見 + 工時禁字）");
        sb.AppendLine("- 純技術 / 內部設計議題 → consensus 自決（不丟老闆）");
        sb.AppendLine("- 對 Christ 看到行為 / 業務邏輯 / spec 有影響 → escalate（給推薦答案 + 理由，不只列 A/B/C 三選）");
        sb.AppendLine("- 禁出現「X 天 / Y 週 / X.X 天」工時估算（AiTeam 是 AI Session 模式無「天」概念）");
        sb.AppendLine("- 禁出現「待 Christ 拍板 / 待老闆拍板 / 待 Christ 決定」決策包字串");
        sb.AppendLine("- 規模可用「S / M / L」或「~N00 LOC」表達，不換算工時");
        sb.AppendLine();
    }

    // ============================================================
    //  decision parsers
    // ============================================================

    public static bool TryParseNeedsDemi(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("needs_demi", out var prop))
                    return prop.GetBoolean();
            }
            catch { /* 繼續往上找 */ }
        }
        return true; // 解析失敗時預設需要 Demi（保守策略）
    }

    /// <summary>
    /// Stage 56：FF 四十二 修 — 改 line-iteration + try-deserialize pattern。
    /// 對齊 TryParseDesignPetraDecision (line 288) / TryParseDesignAdjustmentEvaluation (line 305) 既有 helper pattern：
    /// 從輸入頭往下掃，遇到 trim 後 startsWith('[') 的 line 起，把該行 + 後續所有 lines join 起來嘗試 Deserialize；
    /// 失敗則跳到下一個 startsWith('[') 起點重試。處理三個 case：
    ///   ① `[MOCK] 開頭` + 後接合法 array — 第一輪 `[MOCK]...` parse 失敗 → 第二輪真 array 起點 parse 成功
    ///   ② 純 multi-line array — 第一輪 join 全文 parse 成功
    ///   ③ 含字串 `[example]` 嵌套 — 第一輪 `[example]...` parse 失敗 → 跳到真 array 第二輪成功
    /// </summary>
    public static List<DesignIssueDto>? TryParseDesignIssues(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var lines = content.Split('\n');
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith('[')) continue;

            // 從第 i 行起 join 到尾，嘗試解析
            var candidate = string.Join('\n', lines, i, lines.Length - i);
            try
            {
                var result = JsonSerializer.Deserialize<List<DesignIssueDto>>(candidate, options);
                if (result is not null) return result;
            }
            catch { /* 繼續往下找下一個 [ 起點 */ }
        }
        return null;
    }

    public static DesignPetraDecision? TryParseDesignPetraDecision(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 10); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<DesignPetraDecision>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    public static DesignAdjustmentEvaluation? TryParseDesignAdjustmentEvaluation(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<DesignAdjustmentEvaluation>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }
}

// ============================================================
//  internal records（從 legacy DesignMeetingService 搬入）
// ============================================================

/// <summary>Petra round 整理判斷（對齊 legacy DesignMeetingService 內 internal record）。
/// Stage 52：抽到 DesignPrompts.cs 給 framework + legacy 共用 SoT。</summary>
internal record DesignPetraDecision(
    string   Decision,       // "consensus" | "needs_discussion" | "needs_adjustment" | "escalate"
    string   Summary,
    string[] AdjustmentTargets,
    Dictionary<string, string> AdjustmentInstructions,
    string?  EscalateReason);

/// <summary>Petra 評估 Rosa/Demi 調整後的回應（對齊 legacy DesignMeetingService 內 internal record）。</summary>
internal record DesignAdjustmentEvaluation(
    string  Evaluation,      // "approved" | "needs_meeting"
    string? DesignPlan,
    string? Reason);

/// <summary>Rosa 產出 GitHub Issue 結構（對齊 legacy DesignMeetingService 內 internal class）。</summary>
internal class DesignIssueDto
{
    public string       Title  { get; set; } = "";
    public string       Body   { get; set; } = "";
    public List<string> Labels { get; set; } = [];
}
