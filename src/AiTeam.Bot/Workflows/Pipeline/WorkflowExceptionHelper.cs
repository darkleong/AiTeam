namespace AiTeam.Bot.Workflows.Pipeline;

/// <summary>
/// Stage 60-FF 五十五：framework Workflow event 內含 exception 的 unwrap helper。
///
/// 場景：Microsoft.Agents.AI.Workflows 1.x 版本 KickoffAgentExecutor / DesignAgentExecutor 內 throw exception
/// 不直接 propagate，而是包成 WorkflowErrorEvent.Exception 在 watch stream emit。為了讓 MeetingSubprocessFailureException
/// / LlmApiFailureException 兩類「Agent 不可恢復失敗」業務 exception 能從 RunWorkflowAsync watch loop 內
/// re-throw 給上層 catch（fire 第 7 routing），需從 event Exception 走 InnerException chain 抓出原 type。
///
/// 對齊 Aria gate 反饋「framework 1.x ExecutorFailedEvent unwrap 議題 spike 必驗點」精神 —
/// 採穩健 InnerException 走訪 + AggregateException 展開兩條 path，不依賴 framework version-specific API。
/// </summary>
internal static class WorkflowExceptionHelper
{
    /// <summary>從 framework event Exception（可能含 InnerException chain / AggregateException InnerExceptions）找出指定 type。</summary>
    public static T? FindInner<T>(Exception? root) where T : Exception
    {
        if (root is null) return null;

        // 直接 match
        if (root is T direct) return direct;

        // AggregateException 展開
        if (root is AggregateException agg)
        {
            foreach (var inner in agg.InnerExceptions)
            {
                var found = FindInner<T>(inner);
                if (found is not null) return found;
            }
        }

        // InnerException chain
        return FindInner<T>(root.InnerException);
    }
}
