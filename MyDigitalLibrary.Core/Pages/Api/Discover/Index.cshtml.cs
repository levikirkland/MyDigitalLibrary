using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace MyDigitalLibrary.Core.Pages.Api.Discover;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public IEnumerable<object> Items { get; set; } = Enumerable.Empty<object>();

    public async Task OnGetAsync()
    {
        var query = _db.Set<PublicBookEntity>().OrderByDescending(p => p.UpdatedAt).Take(50);
        var list = await query.Select(p => new {
            p.Title,
            Authors = p.Authors,
            CoverUrl = p.CoverPath,
            p.Publisher,
            p.PublishedAt
        }).ToArrayAsync();

        Items = list;
    }
}
