using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAgent.Models;
using OpenAI;
using OpenAI.Chat;

namespace NewsAgent.Summarizer;

/// <summary>
/// Generates newsletter-style summaries by calling OpenAI or Azure OpenAI.
/// </summary>
public class LlmSummarizer(
    IOptions<DigestConfig> config,
    ILogger<LlmSummarizer> logger) : INewsSummarizer
{
    private const string DefaultSystemPrompt =
        "Summarize the following news articles in Hungarian newsletter format.";

    private string? _cachedSystemPrompt;

    /// <inheritdoc />
    public async Task<DigestOutput> SummarizeAsync(List<NewsArticle> articles, CancellationToken cancellationToken = default)
    {
        var llmConfig = config.Value.Llm;

        var chatClient = CreateChatClient(llmConfig);
        var modelLabel = llmConfig.Provider == "azure_openai"
            ? llmConfig.Deployment ?? llmConfig.Model
            : llmConfig.Model;

        logger.LogInformation("Summarizing {Count} articles using {Provider}/{Model}",
            articles.Count, llmConfig.Provider, modelLabel);

        var systemPrompt = LoadSystemPrompt();
        var userPrompt = BuildArticleList(articles);

        var completion = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            ],
            new ChatCompletionOptions { Temperature = 0.3f },
            cancellationToken);

        var htmlContent = completion.Value.Content[0].Text;
        var plainText = StripHtml(htmlContent);

        logger.LogInformation("Generated digest with {Length} characters", htmlContent.Length);

        return new DigestOutput(
            Title: $"News Digest — {DateTime.UtcNow:yyyy-MM-dd}",
            HtmlContent: htmlContent,
            PlainTextContent: plainText,
            GeneratedAt: DateTimeOffset.UtcNow,
            ArticleCount: articles.Count);
    }

    private static ChatClient CreateChatClient(LlmConfig llmConfig) => llmConfig.Provider switch
    {
        "azure_openai" => CreateAzureChatClient(llmConfig),
        "openai" => CreateOpenAiChatClient(llmConfig),
        _ => throw new InvalidOperationException($"Unsupported LLM provider: '{llmConfig.Provider}'. Use 'openai' or 'azure_openai'.")
    };

    private static ChatClient CreateAzureChatClient(LlmConfig llmConfig)
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? llmConfig.Endpoint
            ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured. Set AZURE_OPENAI_ENDPOINT environment variable.");

        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? llmConfig.ApiKey
            ?? throw new InvalidOperationException("Azure OpenAI API key not configured. Set AZURE_OPENAI_API_KEY environment variable.");

        var deployment = llmConfig.Deployment
            ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured. Set llm.deployment in config.yaml.");

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        return client.GetChatClient(deployment);
    }

    private static ChatClient CreateOpenAiChatClient(LlmConfig llmConfig)
    {
        var apiKey = Environment.GetEnvironmentVariable("NEWS_AGENT_LLM_API_KEY") ?? llmConfig.ApiKey
            ?? throw new InvalidOperationException("OpenAI API key not configured. Set NEWS_AGENT_LLM_API_KEY environment variable.");

        var client = new OpenAIClient(apiKey);
        return client.GetChatClient(llmConfig.Model);
    }

    private static string BuildArticleList(List<NewsArticle> articles)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Articles ({articles.Count}):");
        sb.AppendLine();
        for (var i = 0; i < articles.Count; i++)
        {
            var a = articles[i];
            sb.AppendLine($"[{i + 1}] {a.Title}");
            sb.AppendLine($"    Source: {a.Source}");
            sb.AppendLine($"    URL: {a.Url}");
            sb.AppendLine($"    Published: {a.PublishedAt:yyyy-MM-dd HH:mm} UTC");
            if (a.Summary is not null)
                sb.AppendLine($"    Snippet: {a.Summary}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private string LoadSystemPrompt()
    {
        if (_cachedSystemPrompt is not null)
            return _cachedSystemPrompt;

        var promptFile = config.Value.SystemPromptFile;

        if (string.IsNullOrWhiteSpace(promptFile))
        {
            logger.LogInformation("No system_prompt_file configured, using default prompt");
            _cachedSystemPrompt = DefaultSystemPrompt;
            return _cachedSystemPrompt;
        }

        logger.LogInformation("Resolving system prompt file: {Path}", promptFile);

        if (!File.Exists(promptFile))
        {
            logger.LogWarning("System prompt file not found at {Path}, using default prompt", promptFile);
            _cachedSystemPrompt = DefaultSystemPrompt;
            return _cachedSystemPrompt;
        }

        _cachedSystemPrompt = File.ReadAllText(promptFile);
        var preview = _cachedSystemPrompt.Length > 50
            ? _cachedSystemPrompt[..50] + "..."
            : _cachedSystemPrompt;
        logger.LogInformation("Loaded system prompt from {Path} ({Length} chars): {Preview}",
            promptFile, _cachedSystemPrompt.Length, preview);
        return _cachedSystemPrompt;
    }

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty).Trim();
}
