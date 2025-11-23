using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public interface IFormatService
{
    Task<IEnumerable<FormatEntity>> GetFormatsByBookIdAsync(int bookId);
    Task<FormatEntity?> GetFormatAsync(int bookId, string format);
    Task<FormatEntity> AddFormatAsync(FormatEntity format);
}
