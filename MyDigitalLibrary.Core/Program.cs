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

// NOTE: Do not register middleware types that require the RequestDelegate constructor parameter with DI. They should be added to the pipeline with UseMiddleware<T>().

// If started with "smoketest" run a programmatic smoke test and exit
if (args.Length > 0 && args[0].Equals("smoketest", StringComparison.OrdinalIgnoreCase))
{
    var testPath = builder.Configuration["SMOKETEST_PATH"] ?? (args.Length > 1 ? args[1] : null);
    if (string.IsNullOrEmpty(testPath) || !Directory.Exists(testPath))
    {
        Console.WriteLine("Usage: dotnet run -- smoketest <calibre-folder-path>  OR set SMOKETEST_PATH in configuration.");
        return 0;
    }

    var appForTest = builder.Build();
    using var scope = appForTest.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<CalibreImporter>();
    var testLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        testLogger.LogInformation("Starting smoketest import for {Path}", testPath);
        var result = await importer.ImportFromDirectoryAsync(testPath, userId: 1, importCovers: true, cancellation: CancellationToken.None);
        Console.WriteLine($"Smoketest completed: Imported={result.Imported}, Skipped={result.Skipped}");
        return 0;
    }
    catch (Exception ex)
    {
        testLogger.LogError(ex, "Smoketest failed");
        Console.WriteLine("Smoketest failed: " + ex.Message);
        return 2;
    }
}

var app = builder.Build();

// Log the data protection key path at startup so you can verify it's stable between runs
var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation("DataProtection keys directory: {KeyDir}", keyDirPath);

// Ensure user 1 exists and has admin role (seed)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == 1);
    if (user != null && user.Role != "admin")
    {
        user.Role = "admin";
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded user {UserId} as admin", user.Id);
    }
}
catch (Exception ex)
{
    appLogger.LogWarning(ex, "Failed to seed admin user");
}

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