using ApiStudio.Domain.Enums;

namespace ApiStudio.Domain.Entities;

public class AssertionRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AssertionTarget Target { get; set; } = AssertionTarget.StatusCode;
    public string PropertyName { get; set; } = string.Empty; // e.g. Header name or JSON path
    public AssertionOperator Operator { get; set; } = AssertionOperator.Equals;
    public string ExpectedValue { get; set; } = "200";
    public bool IsEnabled { get; set; } = true;

    public AssertionRule Clone()
    {
        return new AssertionRule
        {
            Id = Guid.NewGuid(),
            Target = Target,
            PropertyName = PropertyName,
            Operator = Operator,
            ExpectedValue = ExpectedValue,
            IsEnabled = IsEnabled
        };
    }
}

public class AssertionResult
{
    public AssertionRule Rule { get; set; } = new();
    public bool Passed { get; set; }
    public string? ActualValue { get; set; }
    public string Message { get; set; } = string.Empty;

    public AssertionResult() { }

    public AssertionResult(AssertionRule rule, bool passed, string? actualValue, string message)
    {
        Rule = rule;
        Passed = passed;
        ActualValue = actualValue;
        Message = message;
    }
}
