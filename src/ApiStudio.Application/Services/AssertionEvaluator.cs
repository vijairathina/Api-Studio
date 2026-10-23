using System.Text.Json;
using System.Text.RegularExpressions;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;

namespace ApiStudio.Application.Services;

public class AssertionEvaluator : IAssertionEvaluator
{
    public List<AssertionResult> Evaluate(ApiResponse response, IEnumerable<AssertionRule> rules)
    {
        var results = new List<AssertionResult>();

        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            var result = EvaluateSingle(response, rule);
            results.Add(result);
        }

        return results;
    }

    private AssertionResult EvaluateSingle(ApiResponse response, AssertionRule rule)
    {
        try
        {
            string? actualValue = null;
            bool passed = false;

            switch (rule.Target)
            {
                case AssertionTarget.StatusCode:
                    actualValue = response.StatusCode.ToString();
                    passed = CompareNumeric(response.StatusCode, rule.Operator, rule.ExpectedValue);
                    break;

                case AssertionTarget.ResponseTimeMs:
                    actualValue = response.ElapsedMilliseconds.ToString();
                    passed = CompareNumeric(response.ElapsedMilliseconds, rule.Operator, rule.ExpectedValue);
                    break;

                case AssertionTarget.Header:
                    var header = response.Headers.FirstOrDefault(h => 
                        string.Equals(h.Key, rule.PropertyName, StringComparison.OrdinalIgnoreCase));
                    actualValue = header.Value;
                    passed = CompareString(actualValue ?? string.Empty, rule.Operator, rule.ExpectedValue);
                    break;

                case AssertionTarget.BodyContains:
                    actualValue = response.Body.Length > 100 ? response.Body[..100] + "..." : response.Body;
                    passed = CompareString(response.Body, rule.Operator, rule.ExpectedValue);
                    break;

                case AssertionTarget.JsonPath:
                    actualValue = ExtractJsonProperty(response.Body, rule.PropertyName);
                    passed = CompareString(actualValue ?? string.Empty, rule.Operator, rule.ExpectedValue);
                    break;
            }

            var message = passed 
                ? $"Passed: {FormatRuleDescription(rule)}" 
                : $"Failed: {FormatRuleDescription(rule)} (Actual: '{actualValue}')";

            return new AssertionResult(rule, passed, actualValue, message);
        }
        catch (Exception ex)
        {
            return new AssertionResult(rule, false, null, $"Error evaluating rule: {ex.Message}");
        }
    }

    private static bool CompareNumeric(long actual, AssertionOperator op, string expectedStr)
    {
        if (!long.TryParse(expectedStr, out var expected))
            return false;

        return op switch
        {
            AssertionOperator.Equals => actual == expected,
            AssertionOperator.NotEquals => actual != expected,
            AssertionOperator.GreaterThan => actual > expected,
            AssertionOperator.LessThan => actual < expected,
            AssertionOperator.Contains => actual.ToString().Contains(expectedStr),
            AssertionOperator.DoesNotContain => !actual.ToString().Contains(expectedStr),
            _ => false
        };
    }

    private static bool CompareString(string actual, AssertionOperator op, string expected)
    {
        return op switch
        {
            AssertionOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            AssertionOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            AssertionOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            AssertionOperator.DoesNotContain => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string? ExtractJsonProperty(string json, string path)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement;

            // Trim leading $. or /
            var cleanPath = path.TrimStart('$', '.', '/');
            var segments = cleanPath.Split(new[] { '.', '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var prop))
                {
                    current = prop;
                }
                else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
                {
                    if (index >= 0 && index < current.GetArrayLength())
                    {
                        current = current[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => current.GetRawText()
            };
        }
        catch
        {
            return null;
        }
    }

    private static string FormatRuleDescription(AssertionRule rule)
    {
        var targetDesc = rule.Target switch
        {
            AssertionTarget.StatusCode => "Status code",
            AssertionTarget.ResponseTimeMs => "Response time (ms)",
            AssertionTarget.Header => $"Header '{rule.PropertyName}'",
            AssertionTarget.BodyContains => "Response body",
            AssertionTarget.JsonPath => $"JSON path '{rule.PropertyName}'",
            _ => rule.Target.ToString()
        };

        var opDesc = rule.Operator switch
        {
            AssertionOperator.Equals => "equals",
            AssertionOperator.NotEquals => "does not equal",
            AssertionOperator.Contains => "contains",
            AssertionOperator.DoesNotContain => "does not contain",
            AssertionOperator.GreaterThan => "is greater than",
            AssertionOperator.LessThan => "is less than",
            _ => rule.Operator.ToString()
        };

        return $"{targetDesc} {opDesc} '{rule.ExpectedValue}'";
    }
}
