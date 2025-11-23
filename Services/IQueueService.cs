namespace MyDigitalLibrary.Services;

public interface IQueueService
{
    Task SendJobMessageAsync(string jobId);
}
