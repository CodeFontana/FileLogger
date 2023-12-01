using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FileLoggerDemo;
public class App : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<App> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public App(IHostApplicationLifetime hostApplicationLifetime,
               ILogger<App> logger)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hostApplicationLifetime.ApplicationStarted.Register(async () =>
        {
            try
            {
                await Task.Yield(); // https://github.com/dotnet/runtime/issues/36063
                await Task.Delay(1000); // Additional delay for Microsoft.Hosting.Lifetime messages
                Execute();
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

    public void Execute()
    {
        _logger.LogTrace("Hello, Trace!");
        _logger.LogDebug("Hello, Debug!");
        _logger.LogInformation("Hello, World!");
        _logger.LogWarning("Hello, Warning!");
        _logger.LogError("Hello, Error!");
        _logger.LogCritical(new Exception("Meltdown imminent!!"), "Hello, Critical!");

        var weatherForecast = new
        {
            Date = DateTime.Now.ToShortDateString(),
            Location = "Center Moriches",
            TemperatureCelsius = 20,
            Summary = "Nice"
        };

        string message = JsonSerializer.Serialize(weatherForecast, _jsonOptions);
        _logger.LogInformation("{message}", message);
    }
}
