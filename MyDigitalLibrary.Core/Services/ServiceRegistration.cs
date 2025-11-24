using Microsoft.Extensions.DependencyInjection;

namespace MyDigitalLibrary.Core.Services;

public static class ServiceRegistration
{
    public static void AddMyCoreServices(this IServiceCollection services)
    {
        // Register GoogleBooksService with a named HttpClient
        services.AddHttpClient<IGoogleBooksService, GoogleBooksService>(client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "MyDigitalLibrary/1.0");
        });

        // Other service registrations can be placed here if desired.
    }
}
