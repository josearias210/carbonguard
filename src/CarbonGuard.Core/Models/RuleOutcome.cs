namespace CarbonGuard.Core.Models;

public enum RuleOutcome
{
    Passed = 0,
    NotEvaluated = 1,
    ExplainedByContext = 2,
    Flagged = 3,
}

