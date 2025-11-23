using Azure.Messaging.ServiceBus;
using MyDigitalLibrary.Entities;
using System.Text.Json;

namespace MyDigitalLibrary.Services;

public class WorkerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WorkerService> _log;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;
    private ServiceBusClient? _sbClient;
    private ServiceBusProcessor? _sbProcessor;

    public WorkerService(IServiceProvider services, ILogger<WorkerService> log, IHostEnvironment env, IConfiguration config)
    {
        _services = services;
        _log = log;
        _env = env;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Worker started");

        var sbConn = _config["AZURE_SERVICEBUS_CONNECTIONSTRING"] ?? _config["SERVICEBUS_CONNECTIONSTRING"] ?? string.Empty;
        var queueName = _config["AZURE_SERVICEBUS_QUEUE_NAME"] ?? _config["SERVICEBUS_QUEUE_NAME"] ?? _config["AZURE_QUEUE_NAME"] ?? "bookshelfworker";

        try
        {
            if (!string.IsNullOrEmpty(sbConn))
            {
                try
                {
                    _sbClient = new ServiceBusClient(sbConn);
                    _sbProcessor = _sbClient.CreateProcessor(queueName, new ServiceBusProcessorOptions { MaxConcurrentCalls = 2, AutoCompleteMessages = false });

                    // Register handlers
                    _sbProcessor.ProcessMessageAsync += ProcessServiceBusMessageAsync;
                    _sbProcessor.ProcessErrorAsync += ErrorHandler;

                    await _sbProcessor.StartProcessingAsync(stoppingToken);
                    _log.LogInformation("ServiceBus processor started for queue {Queue}", queueName);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to start ServiceBus processor");
                    throw; // fail fast if queue configured but couldn't start
                }
            }
            else
            {
                _log.LogWarning("No ServiceBus connection configured; worker will idle.");
            }

            // Wait until cancellation. ServiceBus handlers will run in background.
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
        finally
        {
            // Ensure Service Bus processor is stopped and disposed
            if (_sbProcessor != null)
            {
                try
                {
                    await _sbProcessor.StopProcessingAsync();
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Error stopping ServiceBus processor");
                }

                try
                {
                    await _sbProcessor.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Error disposing ServiceBus processor");
                }
            }

            if (_sbClient != null)
            {
                try
                {
                    await _sbClient.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Error disposing ServiceBus client");
                }
            }

            _log.LogInformation("Worker stopped");
        }
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _log.LogError(args.Exception, "ServiceBus processor error (Entity: {EntityPath}, Operation: {Operation})", args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    private async Task ProcessServiceBusMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var body = message.Body.ToString();
        string? externalJobId = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("jobId", out var jobIdEl)) externalJobId = jobIdEl.GetString();
        }
        catch (JsonException)
        {
            // not JSON, treat body as jobId
            externalJobId = body;
        }

        if (string.IsNullOrEmpty(externalJobId))
        {
            _log.LogWarning("Received ServiceBus message with no jobId: {Body}", body);
            await args.CompleteMessageAsync(message);
            return;
        }

        using var scope = _services.CreateScope();
        var jobSvc = scope.ServiceProvider.GetRequiredService<IJobService>();

        // Atomically attempt to claim the job by external id. If null, job already claimed/processed.
        var claimed = await jobSvc.TryMarkInProgressAsync(externalJobId);
        if (claimed == null)
        {
            _log.LogInformation("Job {ExternalJobId} already claimed/processed; completing message", externalJobId);
            await args.CompleteMessageAsync(message);
            return;
        }

        _log.LogInformation("ServiceBus message claimed job {JobId} (external {ExternalJobId})", claimed.Id, externalJobId);

        try
        {
            // Reuse processing logic on the claimed JobEntity
            if (claimed.JobType == "convert")
            {
                await ProcessConvertJobAsync(scope.ServiceProvider, claimed, args.CancellationToken);
            }
            else if (claimed.JobType == "import")
            {
                await ProcessImportJobAsync(scope.ServiceProvider, claimed, args.CancellationToken);
            }
            else
            {
                await jobSvc.UpdateJobStatusAsync(claimed, "completed", 100, null);
            }

            await args.CompleteMessageAsync(message);
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("ServiceBus job {JobId} cancelled", claimed.Id);
            try { await args.AbandonMessageAsync(message); } catch { }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ServiceBus processing for job {JobId} failed", claimed.Id);
            // On error, abandon so the message can be retried; move to DLQ if persistent failures
            try { await args.AbandonMessageAsync(message); } catch (Exception ex2) { _log.LogWarning(ex2, "Failed to abandon ServiceBus message for job {JobId}", claimed.Id); }

            try { await jobSvc.UpdateJobStatusAsync(claimed, "failed", claimed.Progress, ex.Message); } catch (Exception persEx) { _log.LogWarning(persEx, "Failed to persist failed job status for job {JobId}", claimed.Id); }
        }
    }

    // Extracted convert processing (uses service layer)
    private static async Task ProcessConvertJobAsync(IServiceProvider services, JobEntity job, CancellationToken token)
    {
        var scopeBookSvc = services.GetRequiredService<IBookService>();
        var scopeFormatSvc = services.GetRequiredService<IFormatService>();
        var scopeFileSvc = services.GetRequiredService<IFileService>();
        var scopeStorage = services.GetRequiredService<IStorageService>();
        var scopeJobSvc = services.GetRequiredService<IJobService>();

        var book = job.BookId.HasValue ? await scopeBookSvc.GetBookByIdAsync(job.BookId.Value) : null;
        if (book == null)
        {
            await scopeJobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, "Book not found");
            return;
        }

        string? sourcePath = null;
        if (book.FileId.HasValue)
        {
            var f = await scopeFileSvc.GetFileByIdAsync(book.FileId.Value);
            if (f != null) sourcePath = f.StoragePath;
        }
        if (string.IsNullOrEmpty(sourcePath) && !string.IsNullOrEmpty(book.FilePath)) sourcePath = book.FilePath;

        if (string.IsNullOrEmpty(sourcePath))
        {
            await scopeJobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, "Book file not found");
            return;
        }

        try
        {
            using var src = await scopeStorage.OpenReadAsync(sourcePath);
            using var ms = new MemoryStream();
            await src.CopyToAsync(ms, token);
            ms.Position = 0;

            using var converted = new MemoryStream();
            await ms.CopyToAsync(converted, token);
            converted.Position = 0;

            var targetFormat = "pdf";
            var convertedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_converted.{targetFormat}";
            var storedConverted = await scopeFileSvc.GetOrUploadFileAsync(converted, convertedName, job.UserId);
            var fmtEntity = new FormatEntity { BookId = book.Id, Format = targetFormat, FilePath = storedConverted.StoragePath, FileSize = storedConverted.Size, FileId = storedConverted.Id };
            await scopeFormatSvc.AddFormatAsync(fmtEntity);

            await scopeJobSvc.UpdateJobStatusAsync(job, "completed", 100, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await scopeJobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, ex.Message);
            throw;
        }
    }

    // Extracted import processing
    private static async Task ProcessImportJobAsync(IServiceProvider services, JobEntity job, CancellationToken token)
    {
        var adminSvc = services.GetRequiredService<IAdminService>();
        var bookSvc = services.GetRequiredService<IBookService>();
        var jobSvc = services.GetRequiredService<IJobService>();
        var env = services.GetRequiredService<IHostEnvironment>();
        var importer = services.GetRequiredService<CalibreImporter>();
        var logger = services.GetRequiredService<ILogger<WorkerService>>();

        var user = await adminSvc.GetUserAsync(job.UserId);
        if (user == null)
        {
            await jobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, "User not found for import");
            return;
        }

        // Derive import path from known uploads folder for this job
        var importPath = Path.Combine(env.ContentRootPath, "uploads", "imports", job.JobId);

        if (string.IsNullOrEmpty(importPath) || !Directory.Exists(importPath))
        {
            await jobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, "Import path not found");
            return;
        }

        // Determine importCovers flag from import.json if present
        var importJson = Path.Combine(importPath, "import.json");
        bool importCovers = true;
        if (File.Exists(importJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(importJson));
                if (doc.RootElement.TryGetProperty("importCovers", out var ic)) importCovers = ic.GetBoolean();
            }
            catch (Exception ex)
            {
                await jobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, "Invalid import metadata: " + ex.Message);
                return;
            }
        }

        try
        {
            await jobSvc.UpdateJobStatusAsync(job, "in-progress", 0, null);

            // Run importer (could be long-running)
            var (imported, skipped) = await importer.ImportFromDirectoryAsync(importPath, job.UserId, importCovers, token);

            var summary = $"Imported {imported}, skipped {skipped}.";
            await jobSvc.UpdateJobStatusAsync(job, "completed", 100, summary);
        }
        catch (OperationCanceledException)
        {
            await jobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, "Cancelled");
            throw;
        }
        catch (Exception ex)
        {
            await jobSvc.UpdateJobStatusAsync(job, "failed", job.Progress, ex.Message);
            logger.LogError(ex, "Import job {JobId} failed", job.JobId);
            throw;
        }
        finally
        {
            // cleanup uploaded folder
            try { Directory.Delete(importPath, true); } catch { }
        }
    }
}