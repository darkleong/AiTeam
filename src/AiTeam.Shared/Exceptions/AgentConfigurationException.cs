namespace AiTeam.Shared.Exceptions;

public class AgentConfigurationException(string message = "Agent 設定異常，請確認 Agent 是否存在") : Exception(message);
