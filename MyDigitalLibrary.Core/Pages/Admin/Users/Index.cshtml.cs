using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;
using Microsoft.AspNetCore.Authorization;

namespace MyDigitalLibrary.Core.Pages.Admin.Users;

[Authorize(Roles = "admin")]
public class IndexModel : PageModel
{
    private readonly IAdminService _admin;
    private readonly IBookService _bookService;
    public IndexModel(IAdminService admin, IBookService bookService) => (_admin, _bookService) = (admin, bookService);

    public IEnumerable<UserEntity> Users { get; set; } = Enumerable.Empty<UserEntity>();
    public Dictionary<int, int> BookCounts { get; set; } = new Dictionary<int, int>();

    public async Task OnGetAsync()
    {
        Users = await _admin.GetAllUsersAsync();
        foreach (var u in Users)
        {
            try
            {
                var books = await _bookService.GetBooksByUserIdAsync(u.Id);
                BookCounts[u.Id] = books?.Length ?? 0;
            }
            catch
            {
                BookCounts[u.Id] = 0;
            }
        }
    }
}
