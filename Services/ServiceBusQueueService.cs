using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace MyDigitalLibrary.Services;

public class ServiceBusQueueService : IQueueService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    public ServiceBusQueueService(ServiceBusClient client, string queueName)
    {
        _client = client;
        _sender = _client.CreateSender(queueName);
    }

    public async Task SendJobMessageAsync(string jobId)
    {
        var payload = JsonSerializer.Serialize(new { jobId });
        var message = new ServiceBusMessage(payload);
        await _sender.SendMessageAsync(message);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}
