using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MyDigitalLibrary.Core.Pages.Books;

public class UploadModel : PageModel
{
    private readonly IFileService _fileService;
    private readonly IBookService _bookService;
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public UploadModel(IFileService fileService, IBookService bookService, IAuthService authService, IConfiguration config)
    {
        _fileService = fileService;
        _bookService = bookService;
        _authService = authService;
        _config = config;
    }

    [BindProperty]
    public IFormFile? File { get; set; }

    [BindProperty]
    public IFormFile? Cover { get; set; }

    [BindProperty]
    public string? Title { get; set; }

    [BindProperty]
    public string? Authors { get; set; }

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        if (File == null || File.Length == 0)
        {
            ErrorMessage = "Please select a file to upload.";
            return Page();
        }

        try
        {
            var filename = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(File.FileName)}";

            // Upload original to private container
            var originalsContainer = _config["AZURE_STORAGE_CONTAINER_ORIGINALS"] ?? "originals";
            var fileEntity = await _fileService.GetOrUploadFileAsync(File.OpenReadStream(), filename, userId, originalsContainer);

            string? coverPath = null;
            int? coverFileId = null;
            if (Cover != null && Cover.Length > 0)
            {
                // Generate thumbnail using ImageSharp
                using var image = await Image.LoadAsync(Cover.OpenReadStream());
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(300, 450), Mode = ResizeMode.Max }));
                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms);
                ms.Position = 0;

                var coverName = $"thumb_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(Cover.FileName)}";
                var coversContainer = _config["AZURE_STORAGE_CONTAINER_COVERS"] ?? "cover-thumbnails";

                // Upload thumbnail to public covers container via file service
                var coverEntity = await _fileService.GetOrUploadFileAsync(ms, coverName, userId, coversContainer);
                coverPath = coverEntity.StoragePath;
                coverFileId = coverEntity.Id;
            }

            var bookEntity = new BookEntity
            {
                UserId = userId,
                Title = string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(File.FileName) : Title,
                Authors = Authors,
                OriginalFilename = File.FileName,
                FilePath = fileEntity.StoragePath,
                FileSize = File.Length,
                MimeType = File.ContentType,
                CoverPath = coverPath,
                FileId = fileEntity.Id,
                CoverFileId = coverFileId
            };

            var created = await _bookService.CreateBookAsync(bookEntity);
            Success = true;
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}
