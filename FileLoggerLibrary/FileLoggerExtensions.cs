using FileLoggerLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Logging;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName)
    {
        builder.Services.AddSingleton(new FileLoggerProvider(logName));
        builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
        builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder)
    {
        builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder));
        builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
        builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder, long logMaxBytes)
    {
        builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder, logMaxBytes));
        builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
        builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder, long logMaxBytes, uint logMaxCount)
    {
        builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder, logMaxBytes, logMaxCount));
        builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
        builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
        return builder;
    }

    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, IConfiguration configuration, Action<FileLoggerOptions> configure = null)
    {
        FileLoggerProvider fileLoggerProvider = CreateFromConfiguration(configuration, configure = null);

        if (fileLoggerProvider != null)
        {
            builder.Services.AddSingleton(fileLoggerProvider);
            builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(fileLoggerProvider.LogName));
        }

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

        string logName = fileLogger["LogName"];

        if (string.IsNullOrWhiteSpace(logName) == false)
        {
            options.LogName = logName;
        }
        else
        {
            return null;
        }

        string logFolder = fileLogger["LogFolder"];

        if (string.IsNullOrWhiteSpace(logFolder) == false)
        {
            options.LogFolder = logFolder;
        }

        string logMaxBytes = fileLogger["LogMaxBytes"];

        if (string.IsNullOrWhiteSpace(logMaxBytes) == false && long.TryParse(logMaxBytes, out long maxBytes))
        {
            options.LogMaxBytes = maxBytes;
        }

        string logMaxCount = fileLogger["LogMaxCount"];

        if (string.IsNullOrWhiteSpace(logMaxCount) == false && uint.TryParse(logMaxCount, out uint maxFiles))
        {
            options.LogMaxCount = maxFiles;
        }

        if (configure != null)
        {
            configure(options);
        }

        return new FileLoggerProvider(options);
    }
}
