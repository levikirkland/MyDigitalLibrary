using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDigitalLibrary.Core.Services;

public class FormatService : IFormatService
{
    private readonly AppDbContext _db;
    public FormatService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<FormatEntity>> GetFormatsByBookIdAsync(int bookId)
    {
        return await _db.Formats.Where(f => f.BookId == bookId).ToListAsync();
    }

    public async Task<FormatEntity?> GetFormatAsync(int bookId, string format)
    {
        return await _db.Formats.FirstOrDefaultAsync(f => f.BookId == bookId && f.Format.ToLower() == format.ToLower());
    }

    public async Task<FormatEntity> AddFormatAsync(FormatEntity format)
    {
        _db.Formats.Add(format);
        await _db.SaveChangesAsync();
        return format;
    }
}
