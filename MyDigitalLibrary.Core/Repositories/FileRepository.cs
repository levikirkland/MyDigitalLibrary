using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Repositories;

public class FileRepository : IFileRepository
{
    private readonly AppDbContext _db;
    public FileRepository(AppDbContext db) => _db = db;

    public async Task<FileEntity?> GetByShaAsync(string sha) => await _db.Files.FirstOrDefaultAsync(f => f.Sha256 == sha);
    public async Task<FileEntity?> GetByIdAsync(int id) => await _db.Files.FindAsync(id);
    public async Task<FileEntity> AddAsync(FileEntity file) { _db.Files.Add(file); await _db.SaveChangesAsync(); return file; }
    public async Task DeleteAsync(FileEntity file) { _db.Files.Remove(file); await _db.SaveChangesAsync(); }
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
