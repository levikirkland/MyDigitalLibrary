using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Specifications;

public class RuleSpecification : ISpecification<BookEntity>
{
    public Expression<Func<BookEntity, bool>> Criteria { get; }

    public RuleSpecification(Rule rule)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));

        var param = Expression.Parameter(typeof(BookEntity), "b");
        Expression body = Expression.Constant(true);

        // Property access
        var prop = Expression.PropertyOrField(param, rule.ColumnName);

        var valueString = rule.Value ?? string.Empty;

        switch (rule.Operator)
        {
            case RuleOperator.Equals:
                body = Expression.AndAlso(
                    Expression.NotEqual(prop, Expression.Constant(null, typeof(string))),
                    Expression.Equal(prop, Expression.Constant(valueString))
                );
                break;
            case RuleOperator.Like:
                var toLower = Expression.Call(prop, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
                var pattern = Expression.Constant($"%{valueString.ToLowerInvariant()}%", typeof(string));
                var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
                var likeMethod = typeof(DbFunctionsExtensions).GetMethod("Like", new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;
                var likeCall = Expression.Call(likeMethod, efFunctions, toLower, pattern);
                body = Expression.AndAlso(Expression.NotEqual(prop, Expression.Constant(null, typeof(string))), likeCall);
                break;
            case RuleOperator.Contains:
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
                var containsCall = Expression.Call(prop, containsMethod, Expression.Constant(valueString));
                body = Expression.AndAlso(Expression.NotEqual(prop, Expression.Constant(null, typeof(string))), containsCall);
                break;
            case RuleOperator.GreaterThan:
            case RuleOperator.GreaterOrEqual:
            case RuleOperator.LessThan:
            case RuleOperator.LessOrEqual:
                // Build numeric comparison where possible. Convert property to double if possible.
                if (double.TryParse(valueString, out var dv))
                {
                    var constVal = Expression.Constant(dv, typeof(double));
                    // convert prop to double
                    var propAsDouble = Expression.Convert(prop, typeof(double));
                    Expression compare = Expression.Constant(false);
                    if (rule.Operator == RuleOperator.GreaterThan) compare = Expression.GreaterThan(propAsDouble, constVal);
                    if (rule.Operator == RuleOperator.GreaterOrEqual) compare = Expression.GreaterThanOrEqual(propAsDouble, constVal);
                    if (rule.Operator == RuleOperator.LessThan) compare = Expression.LessThan(propAsDouble, constVal);
                    if (rule.Operator == RuleOperator.LessOrEqual) compare = Expression.LessThanOrEqual(propAsDouble, constVal);

                    body = Expression.AndAlso(Expression.NotEqual(prop, Expression.Constant(null, typeof(object))), compare);
                }
                else
                {
                    body = Expression.Constant(false);
                }
                break;
            default:
                body = Expression.Constant(true);
                break;
        }

        Criteria = Expression.Lambda<Func<BookEntity, bool>>(body, param);
    }
}
