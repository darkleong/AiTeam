namespace AiTeam.Shared.Exceptions;

public class RuleNotFoundException(Guid id)
    : Exception($"規則 ID {id} 不存在或已被刪除");

public class AgentNotFoundException(Guid agentId)
    : Exception($"Agent ID {agentId} 不存在");
