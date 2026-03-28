using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Collector;
using NewsAgent.Delivery;
using NewsAgent.Models;
using NewsAgent.Processor;
using NewsAgent.Summarizer;

namespace NewsAgent.Scheduling;

/// <summary>
/// Background service that runs digest collection cycles on a cron schedule.
/// </summary>
public class DigestWorker(
    IOptions<DigestConfig> config,
    INewsCollector collector,
    IArticleProcessor processor,
    INewsSummarizer summarizer,
    IEnumerable<IDigestDelivery> deliveries,
    ILogger<DigestWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronExpression = CronExpression.Parse(config.Value.Schedule.Cron);
        logger.LogInformation("Digest worker started with schedule: {Schedule}", config.Value.Schedule.Cron);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var nextRun = cronExpression.GetNextOccurrence(now.UtcDateTime);

            if (nextRun is null)
            {
                logger.LogWarning("No next occurrence found for cron expression: {Schedule}", config.Value.Schedule.Cron);
                break;
            }

            var delay = nextRun.Value - now.UtcDateTime;
            logger.LogInformation("Next digest run at {NextRun} (in {Delay})", nextRun, delay);

            await Task.Delay(delay, stoppingToken);

            await RunDigestCycleAsync(stoppingToken);
        }
    }

    private async Task RunDigestCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting digest cycle");

            var articles = await collector.CollectAsync(cancellationToken);
            logger.LogInformation("Collected {Count} raw articles", articles.Count);

            var processed = await processor.ProcessAsync(articles, cancellationToken);
            logger.LogInformation("Processed to {Count} articles", processed.Count);

            if (processed.Count == 0)
            {
                logger.LogWarning("No articles to summarize, skipping cycle");
                return;
            }

            var digest = await summarizer.SummarizeAsync(processed, cancellationToken);

            foreach (var delivery in deliveries)
            {
                try
                {
                    await delivery.DeliverAsync(digest, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Delivery failed: {DeliveryType}", delivery.GetType().Name);
                }
            }

            logger.LogInformation("Digest cycle completed successfully with {Count} articles", digest.ArticleCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Digest cycle failed");
        }
    }
}
