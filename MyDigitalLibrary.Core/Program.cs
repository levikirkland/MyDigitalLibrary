using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Repositories; // Add this line for data access
using MyDigitalLibrary.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Load user-secrets in Development so local secrets are available (no effect in Production)
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly(), optional: true);
}

// Add services to the container.
builder.Services.AddRazorPages();

var azureConn = builder.Configuration["AZURE_SQL_CONNECTIONSTRING"];
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(azureConn, sqlOptions => sqlOptions.EnableRetryOnFailure()));

// Simple default cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

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
ServiceRegistration.AddMyCoreServices(builder.Services);

// File and storage services
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFileService, FileService>();
// Choose storage implementation depending on configuration (secrets will be read here in Development)
var blobConn = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"] ?? builder.Configuration.GetConnectionString("AzureStorage");


builder.Services.AddSingleton<IStorageService, AzureBlobStorageService>();


// Register importer
builder.Services.AddScoped<CalibreImporter>();

// Register worker background service (uses AZURE_SERVICEBUS_CONNECTIONSTRING internally)
builder.Services.AddHostedService<WorkerService>();

// Register collection repository/service
builder.Services.AddScoped<MyDigitalLibrary.Core.Repositories.ICollectionRepository, MyDigitalLibrary.Core.Repositories.CollectionRepository>();
builder.Services.AddScoped<MyDigitalLibrary.Core.Services.ICollectionService, MyDigitalLibrary.Core.Services.CollectionService>();

// Register review service
builder.Services.AddScoped<MyDigitalLibrary.Core.Services.IReviewService, MyDigitalLibrary.Core.Services.ReviewService>();

var app = builder.Build();

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