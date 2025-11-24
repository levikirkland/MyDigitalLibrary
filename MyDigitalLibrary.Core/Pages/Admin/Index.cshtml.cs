using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;
using Microsoft.AspNetCore.Authorization;

namespace MyDigitalLibrary.Core.Pages.Admin;

[Authorize(Roles = "admin")]
public class IndexModel : PageModel
{
    private readonly IAdminService _admin;
    public IndexModel(IAdminService admin) => _admin = admin;

    public IEnumerable<UserEntity> Users { get; set; } = Enumerable.Empty<UserEntity>();

    public async Task OnGetAsync()
    {
        Users = await _admin.GetAllUsersAsync();
    }
}
