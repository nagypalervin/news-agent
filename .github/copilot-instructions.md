# News Agent — Copilot Instructions

## Project overview
This is a self-hosted, containerized AI news digest agent built in .NET 10+.
It collects news from multiple sources (RSS, News API), summarizes them using an LLM,
and delivers the digest via email on a configurable schedule.

## Architecture
- **Collector**: Gathers news from RSS feeds and optional web scraping
- **Processor**: Deduplicates articles, filters by relevance and date
- **Summarizer**: Uses LLM (OpenAI / Azure OpenAI / Anthropic) to generate newsletter-style summaries
- **Delivery**: Sends output via SMTP email, webhook, or file export
- **Scheduler**: Runs collection cycles on cron-based schedule (no external dependencies)

## Tech stack
- .NET 10 (C#), console application / worker service
- Configuration: YAML-based (`config.yaml`) parsed with YamlDotNet
- HTTP: HttpClient with IHttpClientFactory
- LLM SDKs: Azure.AI.OpenAI, OpenAI, Anthropic (provider-agnostic via config)
- RSS parsing: CodeHollow.FeedReader or custom XML parsing
- Email: MailKit + MimeKit for SMTP
- Scheduling: Cronos for cron expression parsing + hosted service
- Containerization: Docker + docker-compose
- Logging: Microsoft.Extensions.Logging with Serilog sink
- DI: Microsoft.Extensions.DependencyInjection

## Code style
- Use C# 14 features: primary constructors, collection expressions, raw string literals
- Prefer `record` types for DTOs and configuration models
- Use `async/await` throughout — no blocking calls
- Follow Microsoft naming conventions: PascalCase for public members, _camelCase for private fields
- One class per file, organized by feature folder (Collector/, Processor/, Summarizer/, Delivery/)
- Use `IOptions<T>` pattern for configuration binding
- Prefer interfaces for testability: INewsCollector, IArticleProcessor, INewsSummarizer, IDigestDelivery
- XML doc comments on all public interfaces and methods
- No `Console.WriteLine` — use ILogger<T> everywhere

## Error handling
- Use Result<T> pattern or exceptions with typed exception classes
- All HTTP calls must have timeout, retry (Polly), and proper error logging
- LLM calls must handle rate limiting and fallback gracefully
- Never crash the scheduler loop — log errors and continue to next cycle

## Configuration
- All settings come from `config.yaml`, not hardcoded
- Secrets (API keys, SMTP passwords) come from environment variables
- Config model validated at startup with data annotations
- Example config provided in `config.example.yaml`

## Docker
- Multi-stage Dockerfile: build with SDK image, run with runtime image
- docker-compose.yaml with volume mount for config and data
- Health check endpoint (optional, via minimal API)

## Testing
- Unit tests with xUnit + Moq/NSubstitute
- Integration tests for collectors with recorded HTTP responses
- Test project: NewsAgent.Tests

## Language
- Code, comments, and documentation in English
- The generated news digest content supports multiple languages via config (hu, en, de)
