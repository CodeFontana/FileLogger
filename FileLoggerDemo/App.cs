using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace FileLoggerDemo;
public class App : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _config;
    private readonly ILogger<App> _logger;

    public App(IHostApplicationLifetime hostApplicationLifetime,
               IConfiguration configuration,
               ILogger<App> logger)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _config = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hostApplicationLifetime.ApplicationStarted.Register(async () =>
        {
            try
            {
                await Task.Yield(); // https://github.com/dotnet/runtime/issues/36063
                await Task.Delay(1000); // Additional delay for Microsoft.Hosting.Lifetime messages
                await Run();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception!");
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task Run()
    {
        _logger.LogCritical("Hello, Critical!");
        _logger.LogDebug("Hello, Debug!");
        _logger.LogError("Hello, Error!");
        _logger.LogInformation("Hello, World!");
        _logger.LogTrace("Hello, Trace!");
        _logger.LogWarning("Hello, Warning!");

        Stopwatch watch = Stopwatch.StartNew();

        for (int i = 0; i < 100000; i++)
        {
            _logger.LogInformation(LoremIpsum(6, 20, 4, 8));
        }

        watch.Stop();

        _logger.LogInformation($"Elapsed time: {watch.ElapsedMilliseconds}ms");

        await Task.Delay(1);
    }

    private bool _firstSentence = false;

    public string LoremIpsum(int minWords = 6, int maxWords = 20, int minSentences = 1, int maxSentences = 6)
    {
        var words = new[] {"bacon", "ipsum", "dolor", "amet", "bresola", "tempor", "strip",
                "leberkas", "excepteur", "irure", "hamburger", "alcatra", "veniam", "turkey",
                "est", "exercitation", "in", "brian", "sirloin", "chunk", "tri-tip", "salami",
                "steak", "anim", "chislic", "commodo", "sint", "pastrami", "lorem", "chuck",
                "exercitation", "sunt", "pork", "qui", "chicken", "minim", "voluptate", "ribeye",
                "laborum", "andouille", "elit", "spare ribs", "anim", "cow", "id", "ea", "meatloaf",
                "boudin", "capicola", "adipiscing", "tail", "pork", "belly", "culpa", "shoulder",
                "drumstick", "buffalo", "prochetta", "esse", "beef ribs", "ham hock", "ham", "hock",
                "Consectetur", "occaecat", "fatback", "quis", "fugiat", "biltong", "t-bone",
                "kielbasa", "flank", "voluptate", "pastrami", "ut", "in", "commodo", "adipisicing",
                "proident", "bresaola", "non", "leberkas", "turducken", "enim", "meatball", "laborum",
                "nostrud", "strip steak", "officia", "short ribs", "nulla", "ham", "incididunt",
                "velit", "do", "ex", "dolore", "sunt", "nostrud", "mollit", "bacon", "est",
                "reprehenderit", "landjaeger", "frankfurter", "shoulder", "ground", "round",
                "swine", "pariatur", "susage tri-tip", "aute", "chicken tenderloin", "consequat",
                "venison", "pork belly", "pig tongue", "brisket", "picanha", "ball", "tip",
                "corned beef" };

        Random rand = new();
        int numSentences = rand.Next(maxSentences - minSentences) + minSentences;
        int numWords = rand.Next(maxWords - minWords) + minWords;
        StringBuilder result = new();
        CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
        TextInfo textInfo = cultureInfo.TextInfo;

        if (_firstSentence == false && numSentences > 1 && numWords >= 5)
        {
            result.Append("Bacon ipsum dolor amet ");
            _firstSentence = true;
        }

        for (int s = 0; s < numSentences; s++)
        {
            for (int w = 0; w < numWords; w++)
            {
                if (w == 0)
                {
                    result.Append(textInfo.ToTitleCase(words[rand.Next(words.Length)]));
                }
                else
                {
                    result.Append(words[rand.Next(words.Length)]);
                }

                if (w < numWords - 1)
                {
                    result.Append(' ');
                }
            }

            if (numSentences > 1)
            {
                result.Append(". ");
            }
        }

        return result.ToString();
    }
}
