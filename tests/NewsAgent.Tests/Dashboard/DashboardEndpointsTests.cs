using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewsAgent.Dashboard;
using NewsAgent.Models;

namespace NewsAgent.Tests.Dashboard;

public class DashboardEndpointsTests : IDisposable
{
    private readonly string _tempDir;

    public DashboardEndpointsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"news-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<HttpClient> CreateTestClientAsync(string? outputDir = null)
    {
        var config = new DigestConfig
        {
            Output = new OutputConfig { FilePath = outputDir ?? _tempDir },
            Schedule = new ScheduleConfig { Cron = "0 7 * * 1-5", Timezone = "Europe/Budapest" },
            Dashboard = new DashboardConfig { Enabled = true, Port = 0 }
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Options.Create(config));
        builder.Services.AddSingleton<StatusTracker>();

        var app = builder.Build();
        app.MapDashboardEndpoints();
        await app.StartAsync();

        return app.GetTestClient();
    }

    private void CreateDigestFile(string filename, string content = "<main><h1>Test</h1></main>")
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="hu"><head><title>Test</title></head>
            <body><main>{content}</main></body></html>
            """;
        File.WriteAllText(Path.Combine(_tempDir, filename), html);
    }

    [Fact]
    public async Task Home_ReturnsHtmlWithDigestList()
    {
        CreateDigestFile("digest-2026-03-29-1754.html");
        CreateDigestFile("digest-2026-03-28-0800.html");

        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("Archívum", html);
        Assert.Contains("digest-2026-03-29-1754.html", html);
        Assert.Contains("digest-2026-03-28-0800.html", html);
    }

    [Fact]
    public async Task Home_ShowsEmptyState()
    {
        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("0 digest", html);
    }

    [Fact]
    public async Task DigestView_ReturnsDigestContent()
    {
        CreateDigestFile("digest-2026-03-29-1754.html", "<h1>Test Digest</h1><p>Content here</p>");

        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/digest/digest-2026-03-29-1754.html");

        Assert.True(response.IsSuccessStatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Digest", html);
        Assert.Contains("Content here", html);
        Assert.Contains("Vissza az archívumhoz", html);
    }

    [Fact]
    public async Task DigestView_ReturnsNotFoundForMissing()
    {
        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/digest/digest-9999-01-01-0000.html");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DigestView_RejectsInvalidFilename()
    {
        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/digest/../etc/passwd");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DigestView_HasNavigation()
    {
        CreateDigestFile("digest-2026-03-27-0800.html");
        CreateDigestFile("digest-2026-03-28-0800.html");
        CreateDigestFile("digest-2026-03-29-0800.html");

        using var client = await CreateTestClientAsync();
        var html = await (await client.GetAsync("/digest/digest-2026-03-28-0800.html")).Content.ReadAsStringAsync();

        Assert.Contains("Régebbi", html);
        Assert.Contains("Újabb", html);
        Assert.Contains("digest-2026-03-27-0800.html", html);
        Assert.Contains("digest-2026-03-29-0800.html", html);
    }

    [Fact]
    public async Task DigestRaw_ReturnsFileDownload()
    {
        CreateDigestFile("digest-2026-03-29-1754.html");

        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/digest/digest-2026-03-29-1754.html/raw");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Status_ReturnsValidJson()
    {
        using var client = await CreateTestClientAsync();
        var response = await client.GetAsync("/status");

        Assert.True(response.IsSuccessStatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        Assert.Equal("ok", doc.RootElement.GetProperty("health").GetString());
        Assert.True(doc.RootElement.TryGetProperty("digestsGenerated", out _));
        Assert.True(doc.RootElement.TryGetProperty("lastArticleCount", out _));
    }

    [Fact]
    public async Task Status_ReflectsTrackerState()
    {
        var config = new DigestConfig
        {
            Output = new OutputConfig { FilePath = _tempDir },
            Schedule = new ScheduleConfig { Cron = "0 7 * * 1-5", Timezone = "Europe/Budapest" },
            Dashboard = new DashboardConfig { Enabled = true, Port = 0 }
        };

        var tracker = new StatusTracker();
        tracker.RecordSuccess(7);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Options.Create(config));
        builder.Services.AddSingleton(tracker);

        var app = builder.Build();
        app.MapDashboardEndpoints();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var json = await (await client.GetAsync("/status")).Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        Assert.Equal("ok", doc.RootElement.GetProperty("health").GetString());
        Assert.Equal(7, doc.RootElement.GetProperty("lastArticleCount").GetInt32());
    }

    [Fact]
    public async Task Status_ReportsErrorHealth()
    {
        var config = new DigestConfig
        {
            Output = new OutputConfig { FilePath = _tempDir },
            Schedule = new ScheduleConfig { Cron = "0 7 * * 1-5", Timezone = "Europe/Budapest" },
            Dashboard = new DashboardConfig { Enabled = true, Port = 0 }
        };

        var tracker = new StatusTracker();
        tracker.RecordError("Connection timeout");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Options.Create(config));
        builder.Services.AddSingleton(tracker);

        var app = builder.Build();
        app.MapDashboardEndpoints();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var json = await (await client.GetAsync("/status")).Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        Assert.Equal("error", doc.RootElement.GetProperty("health").GetString());
        Assert.Contains("timeout", doc.RootElement.GetProperty("lastError").GetString());
    }
}
