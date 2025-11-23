using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Azure.Storage.Blobs.Models;

namespace MyDigitalLibrary.Services;

public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _defaultContainer;
    private readonly BlobServiceClient _blobService;
    private readonly string? _accountName;
    private readonly string? _accountKey;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new FileExtensionContentTypeProvider();

    public AzureBlobStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _env = env;
        _config = config;
        var conn = config["AZURE_STORAGE_CONNECTION_STRING"] ?? string.Empty;
        var containerName = config["AZURE_STORAGE_CONTAINER_COVERS"] ?? "bookshelf";
        _blobService = new BlobServiceClient(conn);
        _defaultContainer = _blobService.GetBlobContainerClient(containerName);
        _defaultContainer.CreateIfNotExists();

        // Try to parse account name/key from connection string for SAS generation
        var mName = Regex.Match(conn, "AccountName=([^;]+)", RegexOptions.IgnoreCase);
        var mKey = Regex.Match(conn, "AccountKey=([^;]+)", RegexOptions.IgnoreCase);
        if (mName.Success) _accountName = mName.Groups[1].Value;
        if (mKey.Success) _accountKey = mKey.Groups[1].Value;
    }

    private BlobContainerClient GetContainer(string? containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName)) return _defaultContainer;
        var c = _blobService.GetBlobContainerClient(containerName);
        c.CreateIfNotExists();
        return c;
    }

    public async Task<string> SaveFileAsync(Stream inputStream, string filename, int userId, string? containerName = null)
    {
        var container = GetContainer(containerName);
        var blobName = $"{userId}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{filename}";
        var blob = container.GetBlobClient(blobName);
        inputStream.Position = 0;

        var headers = new BlobHttpHeaders();
        // Determine content type
        if (!_contentTypeProvider.TryGetContentType(filename, out var ct)) ct = "application/octet-stream";
        headers.ContentType = ct;

        // If saving to covers container, set Cache-Control for public caching
        var coversContainer = _config["AZURE_STORAGE_CONTAINER_COVERS"] ?? "cover-thumbnails";
        if (!string.IsNullOrEmpty(containerName) && string.Equals(containerName, coversContainer, StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = "public, max-age=86400";
        }

        // Upload with options
        var options = new BlobUploadOptions { HttpHeaders = headers };
        await blob.UploadAsync(inputStream, options);
        return blob.Uri.ToString();
    }

    public async Task DeleteFileAsync(string path)
    {
        // path is a URI; derive the container and blob name and delete using appropriate container client
        var (containerName, blobName) = ParseContainerAndBlobFromUri(path);
        if (string.IsNullOrEmpty(containerName) || string.IsNullOrEmpty(blobName)) return;
        var container = _blobService.GetBlobContainerClient(containerName);
        var client = container.GetBlobClient(blobName);
        await client.DeleteIfExistsAsync();
    }

    public async Task<Stream> OpenReadAsync(string storagePath)
    {
        var (containerName, blobName) = ParseContainerAndBlobFromUri(storagePath);
        if (string.IsNullOrEmpty(containerName) || string.IsNullOrEmpty(blobName)) throw new FileNotFoundException();
        var container = _blobService.GetBlobContainerClient(containerName);
        var client = container.GetBlobClient(blobName);
        var stream = await client.OpenReadAsync();
        return stream;
    }

    private (string? container, string? blob) ParseContainerAndBlobFromUri(string storagePath)
    {
        try
        {
            var uri = new Uri(storagePath);
            var segs = uri.Segments.Select(s => s.Trim('/')).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            if (segs.Length == 0) return (null, null);
            // first segment is container name
            var container = segs[0];
            var blob = string.Join('/', segs.Skip(1));
            return (container, blob);
        }
        catch
        {
            return (null, null);
        }
    }

    public Task<string> GetDownloadUrlAsync(string storagePath, TimeSpan? expires = null)
    {
        // If we parsed account key, generate SAS; otherwise return direct URI
        var blobUri = new Uri(storagePath);
        var blobClient = new BlobClient(blobUri);
        if (_accountName == null || _accountKey == null) return Task.FromResult(storagePath);

        var expiry = DateTimeOffset.UtcNow.Add(expires ?? TimeSpan.FromMinutes(10));
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = expiry
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var storageSharedKeyCred = new Azure.Storage.StorageSharedKeyCredential(_accountName, _accountKey);
        var sas = sasBuilder.ToSasQueryParameters(storageSharedKeyCred).ToString();
        var uriWithSas = new UriBuilder(blobClient.Uri) { Query = sas }.Uri.ToString();
        return Task.FromResult(uriWithSas);
    }
}
