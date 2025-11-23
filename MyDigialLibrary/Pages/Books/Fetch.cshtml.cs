using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;
using System.Net.Http.Headers;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Storage;

namespace MyDigitalLibrary.Pages.Books;

// Streams the underlying file for a book through the app (used by reader)
[Authorize]
public class FetchModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IFileService _file_service;
    private readonly IStorageService _storage;
    private readonly IConfiguration _config;

    public FetchModel(IBookService bookService, IFileService fileService, IStorageService storage, IConfiguration config)
    {
        _bookService = bookService;
        _file_service = fileService;
        _storage = storage;
        _config = config;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var id = Id;
        var book = await _bookService.GetBookByIdAsync(id);
        if (book == null) return NotFound();

        // Debug endpoint: return JSON diagnostics if ?debug=1
        if (Request.Query.TryGetValue("debug", out var dbg) && dbg == "1")
        {
            object? fe = null;
            object? upstream = null;
            bool canOpenLocal = false;

            if (book.FileId.HasValue)
            {
                var fileEntity = await _file_service.GetFileByIdAsync(book.FileId.Value);
                if (fileEntity != null)
                {
                    fe = new { fileEntity.Id, file_entity = fileEntity.StoragePath, fileEntity.Size };

                    // Try to probe upstream URL if it's absolute
                    if (!string.IsNullOrEmpty(fileEntity.StoragePath) && (fileEntity.StoragePath.StartsWith("http://") || fileEntity.StoragePath.StartsWith("https://")))
                    {
                        try
                        {
                            using var http = new System.Net.Http.HttpClient();
                            var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, fileEntity.StoragePath);
                            var resp = await http.SendAsync(req);
                            upstream = new { StatusCode = (int)resp.StatusCode, Reason = resp.ReasonPhrase, ContentType = resp.Content.Headers.ContentType?.ToString() };
                        }
                        catch (Exception ex)
                        {
                            upstream = new { Error = ex.Message };
                        }
                    }

                    // Try to open via storage abstraction for diagnostics
                    try
                    {
                        var s = await _storage.OpenReadAsync(fileEntity.StoragePath);
                        if (s != null) { canOpenLocal = true; s.Dispose(); }
                    }
                    catch (Exception ex)
                    {
                        if (upstream == null) upstream = new { OpenError = ex.Message };
                    }
                }
            }

            return new JsonResult(new
            {
                Book = new { book.Id, book.OriginalFilename, book.FilePath, book.FileId, book.MimeType },
                FileEntity = fe,
                Upstream = upstream,
                CanOpenLocalStorage = canOpenLocal,
                Config = new { AzureStorageConnectionString = !string.IsNullOrEmpty(_config["AZURE_STORAGE_CONNECTION_STRING"] ?? _config["AZURE_STORAGE_CONNECTIONSTRING"] ?? _config["STORAGE_CONNECTIONSTRING"]) }
            });
        }

        // Prefer file entity when available
        if (book.FileId.HasValue)
        {
            var fe = await _file_service.GetFileByIdAsync(book.FileId.Value);
            if (fe != null)
            {
                // If StoragePath looks like an absolute URL, try Azure SAS proxy first when configured
                if (!string.IsNullOrEmpty(fe.StoragePath) && (fe.StoragePath.StartsWith("http://") || fe.StoragePath.StartsWith("https://")))
                {
                    var prox = await TryProxyAzureBlobWithSasAsync(fe.StoragePath, book);
                    if (prox != null) return prox;
                }

                try
                {
                    var stream = await _storage.OpenReadAsync(fe.StoragePath);
                    if (stream != null)
                    {
                        if (stream.CanSeek) Response.ContentLength = stream.Length;
                        Response.Headers["Accept-Ranges"] = "bytes";
                        Response.Headers["Content-Disposition"] = "inline";

                        var contentType = book.MimeType ?? GetMimeTypeFromFilename(book.OriginalFilename) ?? "application/octet-stream";
                        return new FileStreamResult(stream, contentType) { EnableRangeProcessing = true };
                    }
                }
                catch
                {
                    // fallthrough to proxy
                }

                if (!string.IsNullOrEmpty(fe.StoragePath) && (fe.StoragePath.StartsWith("http://") || fe.StoragePath.StartsWith("https://")))
                {
                    return await ProxyRemoteUrlAsync(fe.StoragePath, book);
                }
            }
        }

        if (!string.IsNullOrEmpty(book.FilePath))
        {
            if (book.FilePath.StartsWith("http://") || book.FilePath.StartsWith("https://"))
                return await ProxyRemoteUrlAsync(book.FilePath, book);

            try
            {
                var stream = await _storage.OpenReadAsync(book.FilePath);
                if (stream.CanSeek) Response.ContentLength = stream.Length;
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["Content-Disposition"] = "inline";

                var contentType = book.MimeType ?? GetMimeTypeFromFilename(book.OriginalFilename) ?? "application/octet-stream";
                return new FileStreamResult(stream, contentType) { EnableRangeProcessing = true };
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to open file: " + ex.Message);
            }
        }

        return NotFound();
    }

    // Generate a short-lived SAS for the blob and proxy the blob request server-side using HttpClient (preserves range requests)
    private async Task<IActionResult?> TryProxyAzureBlobWithSasAsync(string url, MyDigitalLibrary.Models.Book book)
    {
        var conn = _config["AZURE_STORAGE_CONNECTION_STRING"] ?? _config["AZURE_STORAGE_CONNECTIONSTRING"] ?? _config["STORAGE_CONNECTIONSTRING"];
        if (string.IsNullOrEmpty(conn)) return null;

        try
        {
            var u = new Uri(url);
            var segments = u.AbsolutePath.TrimStart('/').Split('/', 2);
            if (segments.Length < 2) return null;
            var container = segments[0];
            // AbsolutePath may contain percent-encoding (e.g. spaces -> %20). Decode to get the actual blob name
            var blobName = Uri.UnescapeDataString(segments[1]);

            // extract account name/key from connection string
            string? accountName = null, accountKey = null;
            foreach (var part in conn.Split(';'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var k = kv[0].Trim(); var v = kv[1].Trim();
                if (string.Equals(k, "AccountName", StringComparison.OrdinalIgnoreCase)) accountName = v;
                if (string.Equals(k, "AccountKey", StringComparison.OrdinalIgnoreCase)) accountKey = v;
            }

            if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey)) return null;

            var credential = new StorageSharedKeyCredential(accountName, accountKey);
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = container,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();

            var sasUri = new UriBuilder(u) { Query = sasToken }.ToString();

            // Now proxy using HttpClient so we can forward Range header and avoid CORS
            using var httpClient = new System.Net.Http.HttpClient();
            var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, sasUri);
            if (Request.Headers.TryGetValue("Range", out var ranges)) req.Headers.TryAddWithoutValidation("Range", (string)ranges);

            var resp = await httpClient.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode, "Failed to fetch blob via SAS: " + resp.ReasonPhrase);

            var responseStream = await resp.Content.ReadAsStreamAsync();
            if (resp.Content.Headers.ContentLength.HasValue) Response.ContentLength = resp.Content.Headers.ContentLength.Value;
            if (resp.Headers.TryGetValues("Accept-Ranges", out var ar)) Response.Headers["Accept-Ranges"] = string.Join(',', ar); else Response.Headers["Accept-Ranges"] = "bytes";
            Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? book.MimeType ?? "application/octet-stream";
            Response.Headers["Content-Disposition"] = "inline";
            Response.StatusCode = (int)resp.StatusCode;

            return new FileStreamResult(responseStream, Response.ContentType) { EnableRangeProcessing = true };
        }
        catch
        {
            return null;
        }
    }

    private async Task<IActionResult> ProxyRemoteUrlAsync(string url, MyDigitalLibrary.Models.Book book)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();

            var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            if (Request.Headers.TryGetValue("Range", out var ranges)) req.Headers.TryAddWithoutValidation("Range", (string)ranges);

            var resp = await httpClient.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode, "Failed to proxy remote file: " + resp.ReasonPhrase);

            var responseStream = await resp.Content.ReadAsStreamAsync();
            if (resp.Content.Headers.ContentLength.HasValue) Response.ContentLength = resp.Content.Headers.ContentLength.Value;
            if (resp.Headers.TryGetValues("Accept-Ranges", out var ar)) Response.Headers["Accept-Ranges"] = string.Join(',', ar); else Response.Headers["Accept-Ranges"] = "bytes";
            Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? book.MimeType ?? "application/octet-stream";
            Response.Headers["Content-Disposition"] = "inline";
            Response.StatusCode = (int)resp.StatusCode;

            return new FileStreamResult(responseStream, Response.ContentType) { EnableRangeProcessing = true };
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Failed to proxy remote file: " + ex.Message);
        }
    }

    private static string? GetMimeTypeFromFilename(string? filename)
    {
        if (string.IsNullOrEmpty(filename)) return null;
        var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".epub" => "application/epub+zip",
            ".mobi" => "application/x-mobipocket-ebook",
            ".azw3" => "application/vnd.amazon.ebook",
            ".pdf" => "application/pdf",
            _ => null
        };
    }
}
