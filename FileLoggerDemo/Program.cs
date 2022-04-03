using FileLoggerLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FileLoggerDemo;

internal class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            bool isDevelopment = string.IsNullOrEmpty(env) || env.ToLower() == "development";

            await Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(config =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", true, true);
                    config.AddJsonFile($"appsettings.{env}.json", true, true);
                    config.AddUserSecrets<Program>(optional: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddFileLogger("FileLoggerDemo");
                    //logging.AddFileLogger("FileLoggerDemo", $@"{Environment.CurrentDirectory}\log", 50 * 1048576, 10, LogLevel.Trace);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<App>();
                })
                .RunConsoleAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
}
