using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Repositories;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Services;
using System.IO;

namespace MyDigitalLibrary.Core.Tests;

public class RuleSpecificationTests
{
    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        // Use unique DB name per test to avoid cross-test contamination
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IBookRepository, BookRepository>();
        // provide a simple IFileService stub to satisfy BookService constructor if needed
        services.AddScoped<IFileService, TestFileService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task LikeAndEqualsRule_ReturnsExpected()
    {
        var sp = BuildServices();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Books.AddRange(new BookEntity { UserId = 1, Title = "Mystery Book", Tags = "mystery, thriller", Series = "Old House Series", OriginalFilename = "a", FilePath = "p", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                         new BookEntity { UserId = 1, Title = "New Moon", Tags = "fantasy", Series = "New Moon", OriginalFilename = "b", FilePath = "p2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                         new BookEntity { UserId = 2, Title = "Other user book", Tags = "mystery", Series = "Old House Series", OriginalFilename = "c", FilePath = "p3", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IBookRepository>();
        var rules = new Rule[] {
            new Rule { ColumnName = "Tags", Operator = RuleOperator.Like, Value = "mystery" },
            new Rule { ColumnName = "Series", Operator = RuleOperator.Equals, Value = "Old House Series" }
        };

        var res = await repo.GetByRulesAsync(rules, 1);
        Assert.Single(res);
        Assert.Equal("Mystery Book", res[0].Title);
    }

    [Fact]
    public async Task TagOnlyLike_ReturnsMultiple()
    {
        var sp = BuildServices();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Books.AddRange(new BookEntity { UserId = 1, Title = "Mystery Book", Tags = "mystery, thriller", Series = "Old House Series", OriginalFilename = "a", FilePath = "p", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                         new BookEntity { UserId = 1, Title = "The Detective", Tags = "mystery", Series = "Detective Series", OriginalFilename = "b", FilePath = "p2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IBookRepository>();
        var rules = new Rule[] {
            new Rule { ColumnName = "Tags", Operator = RuleOperator.Like, Value = "mystery" }
        };

        var res = await repo.GetByRulesAsync(rules, 1);
        Assert.Equal(2, res.Length);
    }
}

// Minimal IFileService stub to satisfy DI in earlier code that may require it
public class TestFileService : IFileService
{
    public Task DecrementRefCountAsync(int fileId) => Task.CompletedTask;
    public Task<FileEntity> GetOrUploadFileAsync(Stream inputStream, string filename, int userId, string? containerName = null) => Task.FromResult(new FileEntity { Id = 0, Sha256 = "", StoragePath = "", RefCount = 0, CreatedAt = DateTime.UtcNow });
    public Task<FileEntity?> GetFileByHashAsync(string sha256) => Task.FromResult<FileEntity?>(null);
    public Task<FileEntity?> GetFileByIdAsync(int id) => Task.FromResult<FileEntity?>(null);
}
