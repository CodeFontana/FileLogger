using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FileLoggerDemo;
public class App : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _config;
    private readonly ILogger<App> _logger;

    public App(IHostApplicationLifetime hostApplicationLifetime,
               IConfiguration configuration,
               ILogger<App> logger)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _config = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hostApplicationLifetime.ApplicationStarted.Register(async () =>
        {
            try
            {
                await Task.Yield(); // https://github.com/dotnet/runtime/issues/36063
                await Task.Delay(1000); // Additional delay for Microsoft.Hosting.Lifetime messages
                await Run();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception!");
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task Run()
    {
        _logger.LogCritical("Hello, Critical!");
        _logger.LogDebug("Hello, Debug!");
        _logger.LogError("Hello, Error!");
        _logger.LogInformation("Hello, World!");
        _logger.LogTrace("Hello, Trace!");
        _logger.LogWarning("Hello, Warning!");

        var weatherForecast = new
        {
            Date = DateTime.Now.ToShortDateString(),
            Location = "Center Moriches",
            TemperatureCelsius = 20,
            Summary = "Nice"
        };

        _logger.LogInformation(JsonSerializer.Serialize(weatherForecast, new JsonSerializerOptions { WriteIndented = true}));

        await Task.Delay(1);
    }
}
