# FileLogger -- Simple is Good
Simple ILogger implementation, providing asynchronous and configurable File and Console logging capabilities.

## Configuration
Example appsettings.json:
```
{
  "Logging": {
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
  
Using IHostBuilder:
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

## Rolling Log Files
'LogMaxBytes': Specify maximum size, in bytes, of each log file.
'LogMaxCount': Specify maximum log count, before overwritting existing log files.

## Indentation
Turned on by default:
```
2022-04-04--18.10.20|INFO|FileLoggerDemo.App|{
                                               "Date": "4/4/2022",
                                               "Location": "Center Moriches",
                                               "TemperatureCelsius": 20,
                                               "Summary": "Nice"
                                             }
```
  
If turned off:
```
2022-04-04--18.11.19|INFO|FileLoggerDemo.App|{
  "Date": "4/4/2022",
  "Location": "Center Moriches",
  "TemperatureCelsius": 20,
  "Summary": "Nice"
}
```

## Console Logging and Colors
Turned on by default, log messages will also be written to the console with default coloring:
![Console colors](https://user-images.githubusercontent.com/41308769/161640636-d0f3ac33-da06-4e6a-80d4-797e443fa89f.png)

Console colors are customizable via IConfiguration or IHostBuilder implementation.

## License
Copyright 2022, Brian Fontana
Distributed under the MIT license
