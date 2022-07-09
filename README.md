# FileLogger - Simple is Good
[![Nuget Release](https://img.shields.io/nuget/v/CodeFoxtrot.FileLogger?style=for-the-badge)](https://www.nuget.org/packages/CodeFoxtrot.FileLogger/)

* Cross-platform implementation supporting asynchronous Console and File logging.
* Rolling logs with configurable maximum size, maximum count and append of existing log.
* Configurable default minimum log level.
* Single-line, Multi-line or Custom log entry formats.
* Indent multiline messages for easier reading and analysis.
* Configurable color scheme for Console log messages, for easier reading.

![Snag_1609de2d](https://user-images.githubusercontent.com/41308769/177913392-a33cbc7e-5c7b-43b2-922f-ba9e41c34948.png)

### Single-line Format
![Snag_160b55f8](https://user-images.githubusercontent.com/41308769/177913556-a6b144a5-bbea-42cc-ae07-078923368d5a.png)

### Multi-line Format
![Snag_160d922c](https://user-images.githubusercontent.com/41308769/177913819-5c1134bb-0ffd-4cb3-a134-3bf643d9910c.png)

## How to use

### Scenario #1: Quickstart
The following will create 10x50MB rolling logs for 'FileLoggerDemo_x.log" with default settings:
```
using FileLoggerLibrary;

...<omitted>...

.ConfigureLogging((context, builder) =>
  {
    builder.ClearProviders();
    builder.AddFileLogger("FileLoggerDemo");
  })
```

### Scenario #2: Using appsettings.json
  
**appsettings.json** -- all options shown
```
{
  "Logging": {
    "LogLevel": {
    "Default": "Trace",
    "System": "Information",
    "Microsoft": "Error"
    },
    "FileLogger": {
      "LogName": "FileLoggerDemo",
      "LogFolder": "",
      "LogMaxBytes": 52428800,
      "LogMaxCount": 10,
      "MultilineFormat": false,
      "IndentMultilineMessages": true,
      "ConsoleLogging": true,
      "EnableConsoleColors": true,
      "LogLevelColors": {
        "Trace": "Cyan",
        "Debug": "Blue",
        "Information": "Green",
        "Warning": "Yellow",
        "Error": "Red",
        "Critical": "Magenta",
        "None": "White"
      }
    }
  }
}
```
  
**Program.cs** -- full file for complete context
```
using FileLoggerLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                .ConfigureLogging((context, builder) =>
                {
                    builder.ClearProviders();
                    builder.AddFileLogger(context.Configuration);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<MyApp>();
                })
                .RunConsoleAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
}
```
  
### Scenario #3: Using ConfigureLogging
  
**Program.cs**  -- full file for complete context, all FileLoggerOptions shown
```
using FileLoggerLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                .ConfigureLogging((context, builder) =>
                {
                    builder.ClearProviders();
                    builder.AddFileLogger(configure =>
                    {
                        configure.LogName = "FileLoggerDemo";
                        configure.LogFolder = $@"{Environment.CurrentDirectory}\log";
                        configure.LogMaxBytes = 50 * 1048576;
                        configure.LogMaxCount = 10;
                        configure.LogMinLevel = LogLevel.Trace;
                        configure.MultiLineFormat = false;
                        configure.IndentMultilineMessages = true;
                        configure.ConsoleLogging = true;
                        configure.EnableConsoleColors = true;
                        configure.LogLevelColors = new Dictionary<LogLevel, ConsoleColor>()
                        {
                            [LogLevel.Trace] = ConsoleColor.Cyan,
                            [LogLevel.Debug] = ConsoleColor.Blue,
                            [LogLevel.Information] = ConsoleColor.Green,
                            [LogLevel.Warning] = ConsoleColor.Yellow,
                            [LogLevel.Error] = ConsoleColor.Red,
                            [LogLevel.Critical] = ConsoleColor.DarkRed,
                            [LogLevel.None] = ConsoleColor.White
                        };
                    });
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
```

## Indentation 
IndentMultilineMessages=**true**
```
2022-04-04--18.10.20|INFO|FileLoggerDemo.App|{
                                               "Date": "4/4/2022",
                                               "Location": "Center Moriches",
                                               "TemperatureCelsius": 20,
                                               "Summary": "Nice"
                                             }
```
  
IndentMultilineMessages=**false**
```
2022-04-04--18.11.19|INFO|FileLoggerDemo.App|{
  "Date": "4/4/2022",
  "Location": "Center Moriches",
  "TemperatureCelsius": 20,
  "Summary": "Nice"
}
```

Note: The IndentMultilineMessages option is only for the Single-Line message format.

## Roadmap
* Support for Daily, Weekly or Monthly rolling log, up to 1GB maximum single log file.
* Support for UTC timestamps, instead of local time.
* Introduce option for configuring log appending:  
  --> ON by default, but allow it to be turned OFF  
  --> This would allow a new log file to be created with each program execution.

## Reference
https://docs.microsoft.com/en-us/dotnet/core/extensions/custom-logging-provider
