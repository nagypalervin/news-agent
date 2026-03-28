using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NewsAgent.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NewsAgent.Configuration;

/// <summary>
/// Loads and validates configuration from config.yaml.
/// </summary>
public static class ConfigLoader
{
    private static readonly string[] ConfigPaths =
    [
        "/config/config.yaml",
        Path.Combine(Directory.GetCurrentDirectory(), "config.yaml"),
        Path.Combine(AppContext.BaseDirectory, "config.yaml"),
        FindSolutionRoot("config.yaml")
    ];

    private static string FindSolutionRoot(string fileName)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return Path.Combine(dir.FullName, fileName);
            dir = dir.Parent;
        }
        return string.Empty;
    }

    /// <summary>
    /// Loads environment variables from .env file if it exists.
    /// Searches the same locations as config.yaml.
    /// </summary>
    public static void LoadEnv()
    {
        string[] envPaths =
        [
            "/config/.env",
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
            FindSolutionRoot(".env")
        ];

        var envPath = envPaths.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
        if (envPath is not null)
        {
            DotNetEnv.Env.Load(envPath);
        }
    }

    /// <summary>
    /// Loads DigestConfig from the first available config.yaml file.
    /// </summary>
    public static DigestConfig Load(ILogger? logger = null)
    {
        var configPath = ConfigPaths.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                $"config.yaml not found. Searched: {string.Join(", ", ConfigPaths)}");

        logger?.LogInformation("Loading configuration from {Path}", configPath);

        var yaml = File.ReadAllText(configPath);

        // Expand ${ENV_VAR} placeholders before deserializing
        yaml = ExpandEnvironmentVariables(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<DigestConfig>(yaml)
            ?? throw new InvalidOperationException("Failed to deserialize config.yaml");

        // Resolve systemPromptFile relative to config directory
        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        if (config.SystemPromptFile is not null && !Path.IsPathRooted(config.SystemPromptFile))
        {
            config.SystemPromptFile = Path.Combine(configDir, config.SystemPromptFile);
        }

        // Resolve output directory: /output (Docker) or {repo-root}/output
        config.Output.FilePath = ResolveOutputDirectory();
        logger?.LogInformation("Output directory: {OutputPath}", config.Output.FilePath);

        // Override secrets from environment variables
        config = ApplyEnvironmentOverrides(config);

        Validate(config);

        return config;
    }

    private static DigestConfig ApplyEnvironmentOverrides(DigestConfig config)
    {
        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var openaiApiKey = Environment.GetEnvironmentVariable("NEWS_AGENT_LLM_API_KEY");

        if (azureApiKey is not null && config.Llm.Provider == "azure_openai")
        {
            config.Llm.ApiKey = azureApiKey;
        }
        else if (openaiApiKey is not null)
        {
            config.Llm.ApiKey = openaiApiKey;
        }

        return config;
    }

    private static string ExpandEnvironmentVariables(string input) =>
        Regex.Replace(input, @"\$\{(\w+)\}", match =>
        {
            var envVar = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(envVar) ?? match.Value;
        });

    /// <summary>
    /// Resolves the output directory path.
    /// Uses /output when running in Docker, otherwise {repo-root}/output/.
    /// </summary>
    public static string ResolveOutputDirectory()
    {
        if (Directory.Exists("/output"))
            return "/output";

        var outputPath = FindSolutionRoot("output");
        if (!string.IsNullOrEmpty(outputPath))
            return outputPath;

        return Path.GetFullPath("output");
    }

    private static void Validate(DigestConfig config)
    {
        var context = new ValidationContext(config);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(config, context, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Configuration validation failed: {errors}");
        }
    }
}
