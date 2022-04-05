# FileLogger - Simple is Good
* Simple implementation supporting asynchronous Console and File logging.
* Rolling logs with configurable maximum size, maximum count and append of existing log.
* Configurable minimum log level.
* Indent multiline messages for easier reading and analysis.
* Configurable color scheme for Console log messages, for easier reading.

![Console colors](https://user-images.githubusercontent.com/41308769/161640636-d0f3ac33-da06-4e6a-80d4-797e443fa89f.png)

## Configuration
**Example appsettings.json configuration:**
```
{
  "Logging": {
    "LogLevel": {
    "Default": "Debug",
    "System": "Information",
    "Microsoft": "Error"
    },
    "FileLogger": {
      "LogName": "FileLoggerDemo",
      "LogFolder": "",
      "LogMaxBytes": 52428800,
      "LogMaxCount": 10,
      "LogMinLevel": "Trace",
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
  
**Example IHostBuilder implementation:**
```
logging.AddFileLogger(configure =>
  {
    configure.LogName = "FileLoggerDemo";
    configure.LogFolder = $@"{Environment.CurrentDirectory}\log";
    configure.LogMaxBytes = 50 * 1048576;
    configure.LogMaxCount = 10;
    configure.LogMinLevel = LogLevel.Trace;
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

## Roadmap
* Support for Daily, Weekly or Monthly rolling log, up to 1GB maximum single log file.

## Reference
https://docs.microsoft.com/en-us/dotnet/core/extensions/custom-logging-provider
