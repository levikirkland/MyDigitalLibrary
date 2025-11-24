using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Middleware;
using MyDigitalLibrary.Core.Repositories; // Add this line for data access
using MyDigitalLibrary.Core.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Load user secrets so AZURE_SQL_CONNECTIONSTRING can be read from secrets.json in development
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly());

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // enable API controllers

// Read connection string from environment first, then from configuration (which includes user secrets)
var conn = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") ?? builder.Configuration["AZURE_SQL_CONNECTIONSTRING"];

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(conn));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "MyBookShelf.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.HttpOnly = true;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        // options.Cookie.Domain = "yourdomain.com"; // set if needed
    });

// Register MyDigitalLibraryClient and IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add application services
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReadingService, ReadingService>();

// Register AdminService implementation
builder.Services.AddScoped<IAdminService, AdminService>();

// Register FeatureService
builder.Services.AddScoped<IFeatureService, FeatureService>();

// Register Google Books client and other core services
MyDigitalLibrary.Core.Services.ServiceRegistration.AddMyCoreServices(builder.Services);

// File and storage services
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFileService, FileService>();
// Choose storage implementation depending on configuration (secrets will be read here in Development)
var azureConn = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"] ?? builder.Configuration.GetConnectionString("AzureStorage");
if (!string.IsNullOrEmpty(azureConn))
{
    builder.Services.AddSingleton<IStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddSingleton<IStorageService, LocalStorageService>();
}

// Register importer
builder.Services.AddScoped<CalibreImporter>();

// Register worker background service (uses AZURE_SERVICEBUS_CONNECTIONSTRING internally)
builder.Services.AddHostedService<WorkerService>();

// Register collection repository/service
builder.Services.AddScoped<MyDigitalLibrary.Core.Repositories.ICollectionRepository, MyDigitalLibrary.Core.Repositories.CollectionRepository>();
builder.Services.AddScoped<MyDigitalLibrary.Core.Services.ICollectionService, MyDigitalLibrary.Core.Services.CollectionService>();

// Register review service
builder.Services.AddScoped<MyDigitalLibrary.Core.Services.IReviewService, MyDigitalLibrary.Core.Services.ReviewService>();

// Configure data protection keys directory (create folder and allow override via config)
var keysPathFromConfig = builder.Configuration["DataProtection:KeyPath"];
var keyDirPath = !string.IsNullOrEmpty(keysPathFromConfig)
    ? keysPathFromConfig
    : Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");

try
{
    Directory.CreateDirectory(keyDirPath);
}
catch (Exception ex)
{
    // If directory creation fails, continue but log when app builds (logger not available yet). The folder may need manual creation.
}

builder.Services.AddDataProtection()
  .PersistKeysToFileSystem(new DirectoryInfo(keyDirPath))
  .SetApplicationName("MyBookShelf");

var app = builder.Build();

app.UseHttpsRedirection();

// Serve static files from wwwroot so CSS/JS/images are accessible
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// Add claims refresh middleware after authentication so we can re-issue cookie on role change
app.UseMiddleware<ClaimsRefreshMiddleware>();

app.UseAuthorization();

app.MapControllers(); // map API controllers
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
return 0;

// No changes required in Program.cs for new PublicBookEntity registration as it's handled by AppDbContext. Kept for audit.