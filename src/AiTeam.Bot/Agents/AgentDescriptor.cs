namespace AiTeam.Bot.Agents;

/// <summary>
/// Agent 描述子，用於 CEO 系統提示與動態分派。
///
/// Stage 78c：v4 Pipeline framework 整套砍後 IAgentExecutor interface + AgentExecutionResult record + AgentResultType enum 整套砍。
/// AgentDescriptor 仍 v5.5 active — CeoAgentService.ProcessWithClaudeCodeAsync 參數 / CommandHandler + CeoCommandController 構造 caller。
/// </summary>
/// <param name="Name">Agent 名稱（與 DI key 一致）。</param>
/// <param name="Description">Agent 職責描述，供 CEO LLM 判斷分派對象。</param>
public record AgentDescriptor(string Name, string Description);
