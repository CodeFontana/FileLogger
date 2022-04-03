using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name));
        builder.SetMinimumLevel(LogLevel.Trace);
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name, LogLevel minLevel)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name));
        builder.SetMinimumLevel(minLevel);
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name, string folder)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name, folder));
        builder.SetMinimumLevel(LogLevel.Trace);
        return builder;
    }
    
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name, string folder, LogLevel minLevel)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name, folder));
        builder.SetMinimumLevel(minLevel);
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name, string folder, long maxBytes)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name, folder, maxBytes));
        builder.SetMinimumLevel(LogLevel.Trace);
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name, string folder, long maxBytes, uint maxCount)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name, folder, maxBytes, maxCount));
        builder.SetMinimumLevel(LogLevel.Trace);
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string name, string folder, long maxBytes, uint maxCount, LogLevel minLevel)
    {
        builder.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(sp => new FileLoggerProvider(name, folder, maxBytes, maxCount, minLevel));
        builder.SetMinimumLevel(minLevel);
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, IConfiguration configuration, Action<FileLoggerOptions> configure = null)
    {
        builder.ClearProviders();
        FileLoggerProvider fileLoggerProvider = CreateFromConfiguration(configuration, configure = null);

        if (fileLoggerProvider != null)
        {
            builder.Services.AddSingleton<ILoggerProvider>(fileLoggerProvider);
        }

        builder.SetMinimumLevel(fileLoggerProvider.LogMinLevel);
        return builder;
    }

    private static FileLoggerProvider CreateFromConfiguration(IConfiguration configuration, Action<FileLoggerOptions> configure)
    {
        IConfigurationSection fileLogger = configuration.GetSection("FileLogger");

        if (fileLogger == null)
        {
            return null;
        }

        FileLoggerOptions options = new();

        string logName = fileLogger["Name"];

        if (string.IsNullOrWhiteSpace(logName) == false)
        {
            options.LogName = logName;
        }
        else
        {
            return null;
        }

        string logFolder = fileLogger["Folder"];

        if (string.IsNullOrWhiteSpace(logFolder) == false)
        {
            options.LogFolder = logFolder;
        }

        string logMaxBytes = fileLogger["MaxBytes"];

        if (string.IsNullOrWhiteSpace(logMaxBytes) == false && long.TryParse(logMaxBytes, out long maxBytes))
        {
            options.LogMaxBytes = maxBytes;
        }

        string logMaxCount = fileLogger["MaxCount"];

        if (string.IsNullOrWhiteSpace(logMaxCount) == false && uint.TryParse(logMaxCount, out uint maxFiles))
        {
            options.LogMaxCount = maxFiles;
        }

        string minLevel = fileLogger["MinLevel"];

        options.LogMinLevel = minLevel.ToLower() switch
        {
            "trace" => LogLevel.Trace,
            "warning" => LogLevel.Warning,
            "debug" => LogLevel.Debug,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            "none" => LogLevel.None,
            _ => LogLevel.Information,
        };

        configure?.Invoke(options);
        return new FileLoggerProvider(options);
    }
}
