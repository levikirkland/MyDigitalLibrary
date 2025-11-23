namespace MyDigitalLibrary.Core.Models;

public enum RuleOperator
{
    Equals,
    Like,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Contains
}

public class Rule
{
    // e.g. "Tags", "Series", "Title", "Rating", "Status"
    public string ColumnName { get; set; } = string.Empty;
    public RuleOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
}
