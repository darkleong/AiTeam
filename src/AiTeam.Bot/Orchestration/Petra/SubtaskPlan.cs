using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 70：v5.5 Phase 2 Step 4 — Petra hierarchical decomposition + dependency graph 拆解結果 in-memory record。
///
/// 設計核心：
/// - <see cref="SubtaskPlan"/>：Petra 對任務的完整拆解（subtask list + dependency edges）
/// - <see cref="Subtask"/>：單一可獨立 dispatch 給 Talent 的子任務（Id 從 1 起算 / SkillName 對齊 ISkillRegistry）
/// - <see cref="DependencyEdge"/>：subtask 間依賴邊（FromId → ToId / type=Sequential|Nested）
///
/// 範圍守緊（對齊 Roadmap）：
/// - 純 in-memory record（不持久 DB / dispatch 完即丟）
/// - Independent dependency 純設計 surface — 真實 dispatch 仍 sequential（topo sort 後跑 / Phase 3 評估真並行）
/// - Backwards-compatible：simple task 走 <see cref="Linear"/> = 1 subtask 0 deps，topo sort 結果 = 既有 Stage 69 dispatch 順序
/// </summary>
internal enum DependencyType
{
    Sequential,
    Nested,
}

internal sealed record Subtask(int Id, string SkillName, string Description);

internal sealed record DependencyEdge(int FromId, int ToId, DependencyType Type);

internal sealed record SubtaskPlan(
    IReadOnlyList<Subtask> Subtasks,
    IReadOnlyList<DependencyEdge> Dependencies)
{
    /// <summary>空 plan — Parser 解析失敗 / Petra 回 0 subtask 時用。</summary>
    public static SubtaskPlan Empty { get; } = new(Array.Empty<Subtask>(), Array.Empty<DependencyEdge>());

    /// <summary>
    /// Backwards-compatible 線性 chain — 由 Stage 69 既有「Skill 序列字串」轉成 SubtaskPlan。
    /// Stage 74 修根因：Linear semantic = 真正 sequential chain（後 Talent 依賴前 Talent output / BuildNextWorkerInput 把 prior summaries 餵下個）→ 必須加 sequential edges 1→2, 2→3, ..., n-1→n。
    /// 對齊 Stage 74 DAG fan-out level grouping 紀律：Linear chain 每 level 1 subtask = sequential = 0 regression（Trial baseline 3 subtask Cody→Vera→Quinn 行為保留）。
    /// Stage 70 設定 0 deps 是 DAG fan-out 未引入前的設計簡化；Stage 74 LevelGrouping 引入後 Linear 必須真實 sequential 避誤為「全並行 level 0」。
    /// </summary>
    public static SubtaskPlan Linear(IReadOnlyList<string> skills)
    {
        if (skills.Count == 0) return Empty;
        var subs = skills.Select((s, i) => new Subtask(i + 1, s, string.Empty)).ToList();
        var edges = new List<DependencyEdge>(Math.Max(0, skills.Count - 1));
        for (var i = 1; i < skills.Count; i++)
        {
            edges.Add(new DependencyEdge(i, i + 1, DependencyType.Sequential));
        }
        return new SubtaskPlan(subs, edges);
    }
}

/// <summary>
/// Stage 70：v5.5 Phase 2 Step 4 — Petra LLM 回 JSON SubtaskPlan 的解析器。
///
/// 容錯紀律：
/// - 自動去 markdown code fence（```json ... ``` 包裹）
/// - 解析失敗 / 空 subtasks → 回 false + error 非空（caller fallback Linear 起步）
/// - dependency edges 必須指向存在的 subtask Id — 否則 skip 該 edge（不擋整 plan）
/// - 0 crash 紀律：任何 JSON 異常都吞掉轉 error string 回 false
///
/// JSON 格式範例：
/// <code>
/// {
///   "subtasks":[
///     {"id":1,"skill":"code_implementation","description":"..."},
///     {"id":2,"skill":"code_review","description":"..."}
///   ],
///   "dependencies":[
///     {"from":1,"to":2,"type":"sequential"}
///   ]
/// }
/// </code>
/// </summary>
internal static class SubtaskPlanParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    public static bool TryParse(string raw, out SubtaskPlan plan, out string? error)
    {
        plan = SubtaskPlan.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "raw is empty";
            return false;
        }

        var stripped = StripCodeFence(raw.Trim());

        SubtaskPlanDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SubtaskPlanDto>(stripped, JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"JSON parse failed: {ex.Message}";
            return false;
        }

        if (dto is null || dto.Subtasks is null || dto.Subtasks.Count == 0)
        {
            error = "subtasks missing or empty";
            return false;
        }

        var subtasks = new List<Subtask>(dto.Subtasks.Count);
        foreach (var s in dto.Subtasks)
        {
            if (string.IsNullOrWhiteSpace(s.Skill))
            {
                error = $"subtask id={s.Id} has empty skill";
                return false;
            }
            subtasks.Add(new Subtask(s.Id, s.Skill.Trim(), s.Description ?? string.Empty));
        }

        var validIds = subtasks.Select(s => s.Id).ToHashSet();
        var edges = new List<DependencyEdge>();
        if (dto.Dependencies is not null)
        {
            foreach (var d in dto.Dependencies)
            {
                if (!validIds.Contains(d.From) || !validIds.Contains(d.To)) continue;   // skip 失效 edge 不擋整 plan
                if (d.From == d.To) continue;   // 自指向 skip
                var type = string.Equals(d.Type, "nested", StringComparison.OrdinalIgnoreCase)
                    ? DependencyType.Nested
                    : DependencyType.Sequential;
                edges.Add(new DependencyEdge(d.From, d.To, type));
            }
        }

        plan = new SubtaskPlan(subtasks, edges);
        return true;
    }

    private static string StripCodeFence(string s)
    {
        // 去 ```json ... ``` 或 ``` ... ``` 包裹
        if (!s.StartsWith("```")) return s;
        var firstNl = s.IndexOf('\n');
        if (firstNl < 0) return s;
        var inner = s[(firstNl + 1)..];
        var fenceEnd = inner.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceEnd < 0) return inner.Trim();
        return inner[..fenceEnd].Trim();
    }

    private sealed class SubtaskPlanDto
    {
        public List<SubtaskDto>? Subtasks { get; set; }
        public List<DependencyDto>? Dependencies { get; set; }
    }

    private sealed class SubtaskDto
    {
        public int Id { get; set; }
        public string? Skill { get; set; }
        public string? Description { get; set; }
    }

    private sealed class DependencyDto
    {
        public int From { get; set; }
        public int To { get; set; }
        public string? Type { get; set; }
    }
}

/// <summary>
/// Stage 70：v5.5 Phase 2 Step 4 — SubtaskPlan dependency graph topological sort（Kahn's algorithm）。
///
/// 紀律：
/// - 0 dependency edges → 回 Subtask Id 升序（Linear plan 對齊既有 Stage 69 dispatch 順序）
/// - 多個入度 0 候選 → 以 Id 升序 deterministic 取（test 可重現 / 對 Linear plan 結果就是 1,2,3,...）
/// - cycle detected → throw <see cref="InvalidOperationException"/>（plan 無效不 silent fallback）
/// </summary>
internal static class SubtaskPlanTopologicalSort
{
    public static List<int> Sort(SubtaskPlan plan)
    {
        if (plan.Subtasks.Count == 0) return new List<int>();

        var inDegree = plan.Subtasks.ToDictionary(s => s.Id, _ => 0);
        var adj = plan.Subtasks.ToDictionary(s => s.Id, _ => new List<int>());

        foreach (var edge in plan.Dependencies)
        {
            if (!inDegree.ContainsKey(edge.ToId) || !adj.ContainsKey(edge.FromId)) continue;   // defensive — Parser 已 filter / 雙保險
            adj[edge.FromId].Add(edge.ToId);
            inDegree[edge.ToId]++;
        }

        // 入度 0 的 subtask Id 升序入 queue（多候選 deterministic 取小）
        var queue = new PriorityQueue<int, int>();
        foreach (var s in plan.Subtasks)
        {
            if (inDegree[s.Id] == 0) queue.Enqueue(s.Id, s.Id);
        }

        var result = new List<int>(plan.Subtasks.Count);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            result.Add(id);
            foreach (var next in adj[id])
            {
                if (--inDegree[next] == 0) queue.Enqueue(next, next);
            }
        }

        if (result.Count != plan.Subtasks.Count)
        {
            throw new InvalidOperationException(
                $"SubtaskPlan dependency cycle detected — sorted {result.Count} of {plan.Subtasks.Count} subtasks");
        }
        return result;
    }
}

/// <summary>
/// Stage 74：v5.5 Phase 3 Step 8 — SubtaskPlan 依 dependency level 分組（DAG fan-out 並行 dispatch 用）。
///
/// 設計：Kahn-style BFS 分層 — Level 0 = inDegree=0 / Level N+1 = Level N 處理完後 inDegree drop 到 0 的 subtask。
///
/// 紀律：
/// - 0 dependency edges（Linear plan）→ 每 level 1 subtask = caller 自然走 sequential = 0 regression（對齊 Trial baseline 3 subtask 線性場景）
/// - 同 level 多 subtask → caller 用 Task.WhenAll 並行 dispatch（業界 1.4-2.4× speedup）
/// - cycle detected → throw <see cref="InvalidOperationException"/>（對齊 SubtaskPlanTopologicalSort 紀律）
/// - 同 level 內 deterministic 升序（test 可重現）
/// </summary>
internal static class SubtaskPlanLevelGrouping
{
    public static List<List<int>> Group(SubtaskPlan plan)
    {
        if (plan.Subtasks.Count == 0) return new List<List<int>>();

        var inDegree = plan.Subtasks.ToDictionary(s => s.Id, _ => 0);
        var adj = plan.Subtasks.ToDictionary(s => s.Id, _ => new List<int>());
        foreach (var edge in plan.Dependencies)
        {
            if (!inDegree.ContainsKey(edge.ToId) || !adj.ContainsKey(edge.FromId)) continue;
            adj[edge.FromId].Add(edge.ToId);
            inDegree[edge.ToId]++;
        }

        var levels = new List<List<int>>();
        var current = plan.Subtasks
            .Where(s => inDegree[s.Id] == 0)
            .Select(s => s.Id)
            .OrderBy(id => id)
            .ToList();
        var processed = 0;
        while (current.Count > 0)
        {
            levels.Add(current);
            processed += current.Count;
            var next = new List<int>();
            foreach (var id in current)
            {
                foreach (var nb in adj[id])
                {
                    if (--inDegree[nb] == 0) next.Add(nb);
                }
            }
            current = next.OrderBy(id => id).ToList();
        }

        if (processed != plan.Subtasks.Count)
        {
            throw new InvalidOperationException(
                $"SubtaskPlan dependency cycle detected — grouped {processed} of {plan.Subtasks.Count} subtasks");
        }

        return levels;
    }
}
