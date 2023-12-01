using FileLoggerDemo;
using FileLoggerLibrary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    await Host.CreateDefaultBuilder(args)
        .ConfigureLogging((context, builder) =>
        {
            builder.ClearProviders();
            builder.AddFileLogger(context.Configuration);
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
