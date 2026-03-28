# 📰 News Agent

**AI-powered news digest agent.** Collects articles from RSS feeds and news APIs, filters by topic relevance, summarizes with an LLM, and delivers a professional HTML newsletter — on schedule, self-hosted, in a single Docker container.

Built with .NET 10, Azure OpenAI, and zero cloud dependencies beyond an LLM API key.

---

## What it does

1. **Collects** — Pulls articles from 9+ RSS feeds (TechCrunch, The Verge, VentureBeat, official AI company blogs, Hungarian tech portals)
2. **Filters** — Deduplicates by title similarity, filters by topic relevance and publish date
3. **Summarizes** — Sends filtered articles to an LLM (Azure OpenAI / OpenAI) with a customizable system prompt
4. **Delivers** — Outputs a formatted HTML newsletter to a local file (email delivery coming soon)
5. **Schedules** — Runs on a cron schedule or on-demand with `--run-once`

The default configuration produces a daily Hungarian-language AI industry digest, but everything is configurable — topics, sources, language, schedule, LLM provider, and output format.

---

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or [Docker](https://docs.docker.com/get-docker/)
- An Azure OpenAI or OpenAI API key

### Option A: Docker (recommended)

```bash
git clone https://github.com/nagypalervin/news-agent.git
cd news-agent

# Configure
cp config.example.yaml config.yaml    # edit topics, sources, schedule
cp .env.example .env                  # add your API keys

# Run once
docker compose run --rm news-agent -- --run-once

# Run on schedule (background)
docker compose up -d
```

### Option B: Local .NET

```bash
git clone https://github.com/nagypalervin/news-agent.git
cd news-agent

cp config.example.yaml config.yaml
cp .env.example .env

dotnet run --project src/NewsAgent -- --run-once
```

The digest appears in the `output/` directory as `digest-2026-03-28-0700.html`.

---

## Configuration

All settings live in `config.yaml`. Secrets (API keys, passwords) go in `.env`.

### Key settings

| Setting | Description | Default |
|---|---|---|
| `schedule` | Cron expression | `0 7 * * 1-5` (weekdays 7 AM) |
| `timezone` | IANA timezone | `Europe/Budapest` |
| `language` | Digest language | `hu` |
| `rssFeeds` | List of RSS sources | 9 AI/tech feeds |
| `llm.provider` | `azure_openai` or `openai` | `azure_openai` |
| `llm.deployment` | Azure OpenAI deployment name | — |
| `systemPromptFile` | Path to the LLM system prompt | `prompts/ai-news-digest-system.md` |
| `maxArticleAgeHours` | Max article age to include | `36` |

### Environment variables

| Variable | Required | Description |
|---|---|---|
| `AZURE_OPENAI_API_KEY` | Yes* | Azure OpenAI API key |
| `AZURE_OPENAI_ENDPOINT` | Yes* | Azure OpenAI endpoint URL |
| `AZURE_OPENAI_DEPLOYMENT` | Yes* | Azure OpenAI deployment name |
| `OPENAI_API_KEY` | Yes* | OpenAI API key (if using `openai` provider) |
| `NEWS_API_KEY` | No | NewsAPI.org key for broader coverage |

\* Depending on chosen `llm.provider`

### Custom system prompt

The `prompts/ai-news-digest-system.md` file controls the newsletter structure, tone, and formatting rules. Edit it to change the output style — no code changes needed.

---

## Architecture

```
RSS Feeds / News API
        ↓
   ┌─────────┐     ┌───────────┐     ┌────────────┐     ┌──────────┐
   │Collector │ ──→ │ Processor │ ──→ │ Summarizer │ ──→ │ Delivery │
   └─────────┘     └───────────┘     └────────────┘     └──────────┘
   Gather from      Dedup, filter     LLM generates      Save HTML /
   multiple feeds   by topic & date   newsletter          send email
```

Each module has a clean interface (`INewsCollector`, `IArticleProcessor`, `INewsSummarizer`, `IDigestDelivery`) for testability and extensibility.

---

## Adding custom topics

Want a fintech digest instead of AI news? Three changes:

1. **`config.yaml`** — change `rssFeeds` to fintech sources
2. **`config.yaml`** — change topic keywords for relevance filtering
3. **`prompts/`** — create `fintech-digest-system.md` with your newsletter structure

The code stays the same. Config in, newsletter out.

---

## Tech stack

- **.NET 10** (C# 14) — Worker Service pattern
- **Azure OpenAI / OpenAI** — LLM summarization
- **CodeHollow.FeedReader** — RSS parsing
- **YamlDotNet** — Configuration
- **Cronos** — Cron scheduling
- **MailKit** — Email delivery (optional)
- **Serilog** — Structured logging
- **Docker** — Containerization
- **xUnit** — Testing

---

## Project structure

```
news-agent/
├── .github/              # Copilot config, agents, prompt templates
├── prompts/              # LLM system prompts (editable, no code)
├── src/NewsAgent/
│   ├── Collector/        # RSS + News API collection
│   ├── Processor/        # Dedup + topic filtering
│   ├── Summarizer/       # LLM newsletter generation
│   ├── Delivery/         # HTML file + email output
│   ├── Scheduling/       # Cron-based background worker
│   ├── Configuration/    # YAML config loader
│   └── Models/           # Data models
├── tests/NewsAgent.Tests/
├── config.example.yaml
├── .env.example
├── Dockerfile
├── docker-compose.yaml
└── NewsAgent.sln
```

---

## Roadmap

- [x] RSS feed collection
- [x] Topic relevance filtering
- [x] Azure OpenAI summarization
- [x] HTML newsletter output
- [x] Cron scheduling
- [x] Docker support
- [ ] Email delivery (SMTP)
- [ ] Teams / Slack webhook delivery
- [ ] NewsAPI.org integration
- [ ] Web dashboard (digest archive + config editor)
- [ ] Multi-tenant support (multiple topics per instance)
- [ ] Relevance scoring with LLM

---

## Need a custom setup?

This project is built by [Nagypál Ervin](https://nagypalervin.hu) — software engineer specializing in AI agent development for enterprise clients.

If you need:
- Custom topic configuration for your industry
- Integration with your existing systems
- Managed hosting and monitoring
- Enterprise support (SLA, on-premise deployment)

📧 [hello@nagypalervin.hu](mailto:hello@nagypalervin.hu)

---

## Built with

This project was developed using AI-assisted tools: [GitHub Copilot](https://github.com/features/copilot) for code generation and [Claude](https://claude.ai) for architecture planning and product strategy. The `.github/` directory contains the Copilot configuration — fork it and benefit from the same AI-assisted workflow.

---

## License

[MIT](LICENSE) — use it, fork it, build on it.
