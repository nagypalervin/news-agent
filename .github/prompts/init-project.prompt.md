Create the initial .NET project structure for the News Agent application.

## Requirements

1. Create a .NET 10 console application (Worker Service) at `src/NewsAgent/`
2. Create a test project at `tests/NewsAgent.Tests/` using xUnit
3. Create a solution file `NewsAgent.sln` at the root

## Project structure
```
src/NewsAgent/
  Program.cs
  appsettings.json
  Models/
    NewsArticle.cs          — record with Title, Url, Source, PublishedAt, Summary
    DigestConfig.cs         — strongly-typed config model from YAML
    DigestOutput.cs         — the generated newsletter content
  Collector/
    INewsCollector.cs       — interface: Task<List<NewsArticle>> CollectAsync()
    RssCollector.cs         — RSS feed collector using CodeHollow.FeedReader
  Processor/
    IArticleProcessor.cs    — interface: Task<List<NewsArticle>> ProcessAsync(List<NewsArticle>)
    ArticleProcessor.cs     — dedup by title similarity, filter by date
  Summarizer/
    INewsSummarizer.cs      — interface: Task<DigestOutput> SummarizeAsync(List<NewsArticle>)
    LlmSummarizer.cs        — calls OpenAI/Anthropic to generate newsletter
  Delivery/
    IDigestDelivery.cs      — interface: Task DeliverAsync(DigestOutput)
    EmailDelivery.cs        — sends via SMTP using MailKit
  Scheduling/
    DigestWorker.cs         — BackgroundService that runs on cron schedule
  Configuration/
    ConfigLoader.cs         — loads and validates config.yaml using YamlDotNet

tests/NewsAgent.Tests/
  Collector/
    RssCollectorTests.cs
  Processor/
    ArticleProcessorTests.cs
```

## NuGet packages to add
- Microsoft.Extensions.Hosting
- YamlDotNet
- CodeHollow.FeedReader
- MailKit
- Cronos
- Serilog.Extensions.Hosting
- Serilog.Sinks.Console
- Azure.AI.OpenAI (prerelease)
- Polly.Extensions.Http

## Key implementation notes
- Use `Host.CreateDefaultBuilder` with Worker Service pattern
- Load `config.yaml` from current directory or `/config/` mount point
- Register all services in DI
- DigestWorker calculates next run from cron expression and waits
