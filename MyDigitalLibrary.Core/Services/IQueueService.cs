namespace MyDigitalLibrary.Core.Services;

public interface IQueueService
{
    Task SendJobMessageAsync(string jobId);
}
