using System.Linq.Expressions;

namespace MyDigitalLibrary.Core.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}
