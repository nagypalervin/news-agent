Add a new news collector source to the News Agent.

## Context
The project already has `INewsCollector` interface and `RssCollector` implementation.
Each collector returns `List<NewsArticle>` and is registered in DI.

## Task
1. Create a new collector class implementing `INewsCollector`
2. Add any required configuration properties to `DigestConfig`
3. Update `config.example.yaml` with the new source options
4. Register the new collector in DI (collectors are resolved as `IEnumerable<INewsCollector>`)
5. Write unit tests with mocked HTTP responses
6. Handle errors gracefully — if one source fails, others continue

## Naming convention
- Interface: `INewsCollector` (shared)
- Class: `{Source}Collector.cs` (e.g., `NewsApiCollector.cs`, `BingNewsCollector.cs`)
- Tests: `{Source}CollectorTests.cs`
