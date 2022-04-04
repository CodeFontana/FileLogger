# FileLogger -- Simple is Good
Simple ILogger implementation, providing asynchronous and configurable File and Console logging capabilities.

## Configuration
Using appsettings.json...
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
  
Using IHostBuilder...
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
