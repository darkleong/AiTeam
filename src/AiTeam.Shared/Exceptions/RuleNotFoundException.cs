namespace AiTeam.Shared.Exceptions;

public class RuleNotFoundException(string message = "規則不存在或已被刪除") : Exception(message);
