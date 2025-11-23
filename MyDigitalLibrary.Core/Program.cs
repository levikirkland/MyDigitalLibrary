using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Repositories; // Add this line for data access
using MyDigitalLibrary.Core.Services;
using System.Reflection;
using MyDigitalLibrary.Core.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load user secrets in Development so sensitive values (connection strings, keys) can be stored securely
if (builder.Environment.IsDevelopment())
{
    try
    {
        builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
    }
    catch
    {
        // If user-secrets package isn't available or no user secrets configured, continue without failing
    }
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // enable API controllers

// Register EF Core DbContext with SQL Server using the Default connection string from configuration
var conn = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrEmpty(conn))
{
    // Fallback to environment variable or other config
    conn = builder.Configuration["Default"];
}

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

// Register middleware for lock screen before authentication middleware runs
app.UseMiddleware<LockScreenMiddleware>();

// Log the data protection key path at startup so you can verify it's stable between runs
var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation("DataProtection keys directory: {KeyDir}", keyDirPath);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve static files from wwwroot so CSS/JS/images are accessible
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); // map API controllers
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
return 0;

// No changes required in Program.cs for new PublicBookEntity registration as it's handled by AppDbContext. Kept for audit.