using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Core.Entities;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users { get; set; } = default!;
    public DbSet<BookEntity> Books { get; set; } = default!;
    public DbSet<FormatEntity> Formats { get; set; } = default!;
    public DbSet<FileEntity> Files { get; set; } = default!;
    public DbSet<JobEntity> Jobs { get; set; } = default!;
    public DbSet<ReadingProgressEntity> ReadingProgress { get; set; } = default!;
    public DbSet<ReviewEntity> Reviews { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<FormatEntity>().HasIndex(f => new { f.BookId, f.Format }).IsUnique();
        modelBuilder.Entity<FileEntity>().HasIndex(f => f.Sha256).IsUnique();
        modelBuilder.Entity<BookEntity>().HasIndex(b => b.FileId);
        modelBuilder.Entity<BookEntity>().HasIndex(b => b.CoverFileId);
        modelBuilder.Entity<ReadingProgressEntity>().HasIndex(r => new { r.BookId, r.UserId }).IsUnique();
        modelBuilder.Entity<ReviewEntity>().HasIndex(r => new { r.BookId, r.UserId }).IsUnique();
    }
}
