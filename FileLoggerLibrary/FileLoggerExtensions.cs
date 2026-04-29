using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace FileLoggerLibrary;

public static class FileLoggerExtensions
{
    /// <summary>
    /// Registers the file logger provider. When the host has registered an
    /// <see cref="IConfiguration"/> (e.g. via <c>Host.CreateDefaultBuilder</c>),
    /// options are automatically bound from the <c>Logging:FileLogger</c>
    /// section, and the framework's <c>Logging:FileLogger:LogLevel</c>
    /// subsection enables per-provider log level filtering.
    /// </summary>
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddConfiguration();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions<FileLoggerOptions, FileLoggerProvider>(builder.Services);

        // Lower the framework-wide MinLevel floor to match the configured
        // FileLoggerOptions.LogMinLevel when it is more permissive than
        // the framework default (Information). Without this, an option
        // such as LogMinLevel = Trace would still be blocked by the
        // framework's hard floor before reaching this provider.
        builder.Services
            .AddOptions<LoggerFilterOptions>()
            .Configure<IOptionsMonitor<FileLoggerOptions>>((filterOptions, monitor) =>
            {
                LogLevel optionsMinLevel = monitor.CurrentValue.LogMinLevel;
                if (optionsMinLevel < filterOptions.MinLevel)
                {
                    filterOptions.MinLevel = optionsMinLevel;
                }
            });

        return builder;
    }

    /// <summary>
    /// Registers the file logger provider and applies a code-based options
    /// configuration callback on top of any configuration-based bindings.
    /// </summary>
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddFileLogger();
        builder.Services.Configure(configure);

        return builder;
    }

    /// <summary>
    /// Registers the file logger provider and explicitly binds options from
    /// the supplied <paramref name="configuration"/>'s
    /// <c>Logging:FileLogger</c> section. An optional
    /// <paramref name="configure"/> callback runs after binding so code can
    /// override individual values.
    /// </summary>
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, IConfiguration configuration, Action<FileLoggerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.AddFileLogger();
        builder.Services.Configure<FileLoggerOptions>(
            configuration.GetSection("Logging:FileLogger"));

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        return builder;
    }
}
