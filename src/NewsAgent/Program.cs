using Microsoft.Extensions.Options;
using NewsAgent.Collector;
using NewsAgent.Configuration;
using NewsAgent.Delivery;
using NewsAgent.Models;
using NewsAgent.Processor;
using NewsAgent.Scheduling;
using NewsAgent.Summarizer;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

ConfigLoader.LoadEnv();

var runOnce = args.Contains("--run-once");

try
{
    Log.Information("Starting News Agent{Mode}", runOnce ? " (run-once mode)" : "");

    var config = ConfigLoader.Load();

    if (runOnce)
    {
        await RunOnceAsync(config);
    }
    else
    {
        var builder = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                ConfigureServices(services, config);
                RegisterDeliveries(services, config);
                services.AddHostedService<DigestWorker>();
            });

        var host = builder.Build();
        await host.RunAsync();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "News Agent terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void ConfigureServices(IServiceCollection services, DigestConfig config)
{
    services.AddSingleton(Options.Create(config));
    services.AddHttpClient();

    services.AddSingleton<INewsCollector, RssCollector>();
    services.AddSingleton<IArticleProcessor, ArticleProcessor>();
    services.AddSingleton<INewsSummarizer, LlmSummarizer>();
}

static void RegisterDeliveries(IServiceCollection services, DigestConfig config)
{
    services.AddSingleton<IDigestDelivery, FileDelivery>();

    if (config.Email is { Enabled: true })
    {
        services.AddSingleton<IDigestDelivery, EmailDelivery>();
    }

    if (config.Webhooks?.Teams is { Enabled: true })
    {
        services.AddSingleton<IDigestDelivery, TeamsWebhookDelivery>();
    }

    if (config.Webhooks?.Slack is { Enabled: true })
    {
        services.AddSingleton<IDigestDelivery, SlackWebhookDelivery>();
    }
}

static async Task RunOnceAsync(DigestConfig config)
{
    var services = new ServiceCollection();
    services.AddLogging(b => b.AddSerilog());
    ConfigureServices(services, config);
    RegisterDeliveries(services, config);

    await using var sp = services.BuildServiceProvider();

    var collector = sp.GetRequiredService<INewsCollector>();
    var processor = sp.GetRequiredService<IArticleProcessor>();
    var summarizer = sp.GetRequiredService<INewsSummarizer>();
    var deliveries = sp.GetServices<IDigestDelivery>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Collecting articles...");
    var articles = await collector.CollectAsync();
    logger.LogInformation("Collected {Count} raw articles", articles.Count);

    var processed = await processor.ProcessAsync(articles);
    logger.LogInformation("Processed to {Count} articles", processed.Count);

    if (processed.Count == 0)
    {
        logger.LogWarning("No articles to summarize");
        return;
    }

    logger.LogInformation("Summarizing with LLM...");
    var digest = await summarizer.SummarizeAsync(processed);

    foreach (var delivery in deliveries)
    {
        try
        {
            await delivery.DeliverAsync(digest);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Delivery failed: {DeliveryType}", delivery.GetType().Name);
        }
    }

    logger.LogInformation("Done!");
}
