# FileLogger - Simple is Good
[![Nuget Release](https://img.shields.io/nuget/v/CodeFoxtrot.FileLogger?style=for-the-badge)](https://www.nuget.org/packages/CodeFoxtrot.FileLogger/)

* Cross-platform implementation supporting asynchronous Console and File logging.
* Rolling logs with configurable maximum size, maximum count and append of existing log.
* Configurable default minimum log level.
* Single-line, Multi-line or Custom log entry formats.
* Indent multiline messages for easier reading and analysis.
* Configurable color scheme for Console log messages, for easier reading.
* Per-provider log level filtering via `Logging:FileLogger:LogLevel` in `appsettings.json`.
* Live configuration reload via `IOptionsMonitor<FileLoggerOptions>` — runtime-tunable settings (`LogMinLevel`, formatting flags, console colors, custom formatter) update on `appsettings.json` change without restarting the host. File-lifecycle settings (`LogName`, `LogFolder`, `LogMaxBytes`, `LogMaxCount`, `AutoFlush`) are captured at startup.
* `AutoFlush` durability knob — keep the default for per-message durability, or disable for higher throughput under burst load.

## Target frameworks
.NET 8, .NET 9, .NET 10.

![Snag_1609de2d](https://user-images.githubusercontent.com/41308769/177913392-a33cbc7e-5c7b-43b2-922f-ba9e41c34948.png)

### Single-line Format
![Snag_160b55f8](https://user-images.githubusercontent.com/41308769/177913556-a6b144a5-bbea-42cc-ae07-078923368d5a.png)

### Multi-line Format
![Snag_160d922c](https://user-images.githubusercontent.com/41308769/177913819-5c1134bb-0ffd-4cb3-a134-3bf643d9910c.png)

## How to use

### Scenario #1: Quickstart with `appsettings.json` (recommended)
Bind directly from configuration with the no-arg overload — `AddFileLogger()` calls `AddConfiguration()` and registers the options binding for you.

```csharp
using FileLoggerLibrary;

...<omitted>...

.ConfigureLogging((context, builder) =>
{
    builder.ClearProviders();
    builder.AddFileLogger();
})
```

**appsettings.json** -- all options shown
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "FileLogger": {
      "LogLevel": {
        "Default": "Trace",
        "Microsoft.Hosting.Lifetime": "Information"
      },
      "LogName": "FileLoggerDemo",
      "LogFolder": "",
      "LogMaxBytes": 52428800,
      "LogMaxCount": 10,
      "AutoFlush": true,
      "LogMinLevel": "Trace",
      "UseUtcTimestamp": false,
      "MultiLineFormat": false,
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

If `LogName` is omitted, the entry assembly name is used. If `LogFolder` is omitted, a `log` directory under the process's current directory is used.

#### Per-provider log level filtering
The `Logging:FileLogger:LogLevel` section above scopes filters to **just** the FileLogger provider. In the example, the file/console output captures `Trace` and above for application categories, while the global `Logging:LogLevel` keeps every other registered provider at `Information`/`Warning`. The provider's own `LogMinLevel` is also honored, and `AddFileLogger` will lower the framework's global `MinLevel` automatically when needed so trace messages actually reach the dispatcher.

### Scenario #2: `appsettings.json` + an extra inline tweak
Pass an `Action<FileLoggerOptions>` to override or supplement the bound configuration.

```csharp
using FileLoggerLibrary;

...<omitted>...

.ConfigureLogging((context, builder) =>
{
    builder.ClearProviders();
    builder.AddFileLogger(context.Configuration, configure =>
    {
        configure.LogEntryFormatter = msg => $"{msg.TimeStamp} :: {msg.Message}";
    });
})
```

### Scenario #3: Pure code-based configuration
For self-contained apps that don't read `appsettings.json`.

```csharp
using FileLoggerLibrary;

...<omitted>...

.ConfigureLogging((context, builder) =>
{
    builder.ClearProviders();
    builder.AddFileLogger(configure =>
    {
        configure.LogName = "FileLoggerDemo";
        configure.LogFolder = Path.Combine(Environment.CurrentDirectory, "log");
        configure.LogMaxBytes = 50 * 1048576;
        configure.LogMaxCount = 10;
        configure.AutoFlush = true;
        configure.LogMinLevel = LogLevel.Trace;
        configure.UseUtcTimestamp = false;
        configure.MultiLineFormat = false;
        configure.IndentMultilineMessages = true;
        configure.ConsoleLogging = true;
        configure.EnableConsoleColors = true;
        configure.LogLevelColors = new()
        {
            [LogLevel.Trace] = ConsoleColor.Cyan,
            [LogLevel.Debug] = ConsoleColor.Blue,
            [LogLevel.Information] = ConsoleColor.Green,
            [LogLevel.Warning] = ConsoleColor.Yellow,
            [LogLevel.Error] = ConsoleColor.Red,
            [LogLevel.Critical] = ConsoleColor.DarkRed,
            [LogLevel.None] = ConsoleColor.White,
        };
    });
})
```

## Indentation
IndentMultilineMessages=**true**
```text
2026-04-29--18.10.20|INFO|FileLoggerDemo.App|{
                                               "Date": "4/29/2026",
                                               "Location": "Center Moriches",
                                               "TemperatureCelsius": 20,
                                               "Summary": "Nice"
                                             }
```

IndentMultilineMessages=**false**
```text
2026-04-29--18.11.19|INFO|FileLoggerDemo.App|{
  "Date": "4/29/2026",
  "Location": "Center Moriches",
  "TemperatureCelsius": 20,
  "Summary": "Nice"
}
```

Note: The IndentMultilineMessages option is only for the Single-Line message format.

## Debugging
The package ships [Source Link](https://github.com/dotnet/sourcelink) and a matching `.snupkg` symbol package. With **Tools → Options → Debugging → General → Enable Source Link support** turned on (and **Enable Just My Code** turned off), Visual Studio will fetch the exact source revision from GitHub on demand and let you step into FileLogger directly.

## Releases
See [GitHub Releases](https://github.com/CodeFontana/FileLogger/releases) for the changelog.

## Reference
https://docs.microsoft.com/en-us/dotnet/core/extensions/custom-logging-provider
