using System.ComponentModel.DataAnnotations;

namespace NewsAgent.Models;

/// <summary>
/// Root configuration model loaded from config.yaml.
/// </summary>
public class DigestConfig
{
    /// <summary>Topic keywords for relevance filtering.</summary>
    public TopicsConfig Topics { get; set; } = new();

    /// <summary>Sources configuration (RSS feeds, NewsAPI).</summary>
    public SourcesConfig Sources { get; set; } = new();

    /// <summary>Article processing configuration.</summary>
    public ProcessorConfig Processor { get; set; } = new();

    /// <summary>LLM provider configuration.</summary>
    [Required]
    public LlmConfig Llm { get; set; } = new();

    /// <summary>Schedule configuration.</summary>
    public ScheduleConfig Schedule { get; set; } = new();

    /// <summary>Output configuration.</summary>
    public OutputConfig Output { get; set; } = new();

    /// <summary>Email delivery configuration.</summary>
    public EmailConfig? Email { get; set; }

    /// <summary>Path to the system prompt file for LLM summarization.</summary>
    public string? SystemPromptFile { get; set; }
}

/// <summary>
/// Topic keywords for relevance filtering.
/// </summary>
public class TopicsConfig
{
    /// <summary>Primary topic keywords (company names, key terms).</summary>
    public List<string> Primary { get; set; } = [];

    /// <summary>Secondary/broader topic keywords.</summary>
    public List<string> Secondary { get; set; } = [];
}

/// <summary>
/// Sources configuration.
/// </summary>
public class SourcesConfig
{
    /// <summary>RSS feeds to collect articles from.</summary>
    public List<RssFeedConfig> RssFeeds { get; set; } = [];
}

/// <summary>
/// A single RSS feed source.
/// </summary>
public class RssFeedConfig
{
    /// <summary>Feed URL.</summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>Display name for this feed.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Article processor configuration.
/// </summary>
public class ProcessorConfig
{
    /// <summary>Maximum article age in hours.</summary>
    public int MaxAgeHours { get; set; } = 24;

    /// <summary>Maximum articles to send to LLM.</summary>
    public int MaxArticlesForSummary { get; set; } = 20;

    /// <summary>Minimum articles required for a digest.</summary>
    public int MinArticlesRequired { get; set; } = 5;

    /// <summary>Deduplication similarity threshold (0.0-1.0).</summary>
    public double DedupSimilarityThreshold { get; set; } = 0.7;
}

/// <summary>
/// LLM provider configuration.
/// </summary>
public class LlmConfig
{
    /// <summary>Provider name: "openai" or "azure_openai".</summary>
    [Required]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Model name (used for OpenAI provider).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Azure OpenAI deployment name.</summary>
    public string? Deployment { get; set; }

    /// <summary>API key (prefer environment variable).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Azure OpenAI endpoint (prefer AZURE_OPENAI_ENDPOINT env var).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Temperature for LLM generation.</summary>
    public float Temperature { get; set; } = 0.4f;

    /// <summary>Max tokens for LLM response.</summary>
    public int MaxTokens { get; set; } = 4000;
}

/// <summary>
/// Schedule configuration.
/// </summary>
public class ScheduleConfig
{
    /// <summary>Cron expression.</summary>
    public string Cron { get; set; } = "0 7 * * *";

    /// <summary>Timezone (IANA format).</summary>
    public string Timezone { get; set; } = "UTC";
}

/// <summary>
/// Output configuration.
/// </summary>
public class OutputConfig
{
    /// <summary>Output language.</summary>
    public string Language { get; set; } = "hu";

    /// <summary>Output format.</summary>
    public string Format { get; set; } = "newsletter";

    /// <summary>Whether to save digest as HTML file.</summary>
    public bool SaveToFile { get; set; } = true;

    /// <summary>Directory path for file output.</summary>
    public string FilePath { get; set; } = "output/";
}

/// <summary>
/// SMTP email delivery configuration.
/// </summary>
public class EmailConfig
{
    /// <summary>Whether email delivery is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Recipient email addresses.</summary>
    public List<string> To { get; set; } = [];

    /// <summary>Sender email address.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Email subject template.</summary>
    public string Subject { get; set; } = "News Digest — {date}";

    /// <summary>SMTP server hostname.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP server port.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Whether to use SSL/TLS.</summary>
    public bool UseSsl { get; set; } = true;
}
