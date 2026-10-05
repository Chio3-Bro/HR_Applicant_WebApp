using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MyHRExample.Controllers;
using MyHRExample.Models;
using MyHRExample.Service;

const string result = """
{"candidate_name":"Олена <script>alert(1)</script>","status":"completed",
 "skills":["C#","ASP.NET Core"],"confirmed_requirements":["Досвід із .NET"],
 "missing_information":["Рівень англійської"],"summary":"Досвід розробки вебзастосунків.",
 "additional_details":{"experience":"3 роки"}}
""";
var storage = new FakeStorage(result);
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{ ["Analysis:ResultsPrefix"] = "custom-results/" }).Build();
var service = new CandidateAnalysisService(storage, config, NullLogger<CandidateAnalysisService>.Instance);
var candidates = await service.GetCandidatesAsync();
Check(candidates.Count == 5, "Ignore folders and unrelated objects");
var completed = candidates.Single(c => c.ResumeKey.Contains("Olena"));
Check(completed.Status == AnalysisStatus.Completed && completed.Skills.Count == 2, "Parse snake_case analysis");
Check(completed.FullJson!.Contains("additional_details"), "Preserve all additional result fields");
Check(candidates.Single(c => c.ResumeKey.Contains("Waiting")).Status == AnalysisStatus.Pending, "Missing result remains pending");
Check(candidates.Single(c => c.ResumeKey.Contains("Broken")).Status == AnalysisStatus.Failed, "Malformed JSON is not pending");
Check(candidates.Single(c => c.ResumeKey.Contains("Denied")).Status == AnalysisStatus.Unavailable, "S3 denial is not pending");
Check(candidates.Single(c => c.ResumeKey.Contains("Failed")).Status == AnalysisStatus.Failed, "Lambda failure remains failed");
Check(completed.Vacancy == "Backend" && candidates.Single(c => c.ResumeKey.Contains("Waiting")).Name == "Waiting Person", "Recover candidate and vacancy from upload keys");
Check(storage.ReadKeys.All(k => k.StartsWith("custom-results/Backend/") && k.EndsWith(".json")), "Configurable exact result key mapping");
storage.ReadKeys.Clear();
Check((await service.GetCandidatesAsync(completed.ResumeKey)).Count == 1 && storage.ReadKeys.Count == 1, "Details reads only selected result");
Check((await service.GetCandidatesAsync("resume/unknown.pdf")).Count == 0, "Unknown candidate not found");
foreach (var invalid in new[] { "{}", "[]", "{\"status\":\"unexpected\"}", result.Replace("[\"C#\",\"ASP.NET Core\"]", "[123]") })
{
    try { CandidateAnalysisService.ApplyResult(new(), invalid); throw new Exception("Invalid schema accepted"); }
    catch (System.Text.Json.JsonException) { }
}
Check(true, "Reject missing fields, invalid types and unknown statuses");
foreach (var status in new[] { "pending", "processing" })
{
    var candidate = new CandidateAnalysisViewModel();
    CandidateAnalysisService.ApplyResult(candidate, "{\"status\":\"" + status + "\"}");
    Check(candidate.Status == AnalysisStatus.Pending, $"Recognize {status}");
}

// Exercise real MVC routing and compiled Razor views using in-memory storage, without AWS credentials.
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:5187");
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DashboardController).Assembly);
builder.Services.AddSingleton<IAnalysisStorage>(storage);
builder.Services.AddSingleton(service);
await using var app = builder.Build();
app.UseStaticFiles();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
await app.StartAsync();
if (args.Contains("--preview"))
{
    Console.WriteLine("Preview with synthetic data: http://127.0.0.1:5187/Dashboard");
    await app.WaitForShutdownAsync();
    return;
}
try
{
    using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5187") };
    var index = await client.GetStringAsync("/Dashboard");
    Check(index.Contains("HR Dashboard") && index.Contains("candidate-card"), "Dashboard renders candidate cards");
    Check(!index.Contains("<script>alert(1)</script>") && index.Contains("&lt;script&gt;"), "Razor escapes model-generated HTML");
    var detail = await client.GetStringAsync("/Dashboard/Details?key=" + Uri.EscapeDataString(completed.ResumeKey));
    Check(detail.Contains("additional_details") && detail.Contains("raw-analysis"), "Details renders full analysis JSON");
    Check((await client.GetAsync("/Dashboard/Details?key=resume/unknown.pdf")).StatusCode == HttpStatusCode.NotFound, "Unknown details returns HTTP 404");
    Check((await client.GetAsync("/Dashboard/Details")).StatusCode == HttpStatusCode.NotFound, "Missing key returns HTTP 404");
    storage.FailListing = true;
    var error = await client.GetStringAsync("/Dashboard");
    Check(error.Contains("role=\"alert\"") && !error.Contains("dashboard-empty"), "S3 list failure renders error instead of empty state");
    Check((await client.GetAsync("/Dashboard/Details?key=" + Uri.EscapeDataString(completed.ResumeKey))).StatusCode == HttpStatusCode.ServiceUnavailable, "Details S3 failure returns HTTP 503");
    storage.FailListing = false;
    storage.Empty = true;
    Check((await client.GetStringAsync("/Dashboard")).Contains("dashboard-empty"), "Empty bucket renders onboarding state");
}
finally { await app.StopAsync(); }
Console.WriteLine("All dashboard checks passed.");

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine("PASS: " + description);
}

sealed class FakeStorage(string result) : IAnalysisStorage
{
    public bool FailListing { get; set; }
    public bool Empty { get; set; }
    public System.Collections.Concurrent.ConcurrentBag<string> ReadKeys { get; } = [];
    public Task<List<S3Object>> GetFilesAsync(string? prefix = null)
    {
        if (FailListing) throw new AmazonS3Exception("Denied") { StatusCode = HttpStatusCode.Forbidden };
        if (Empty) return Task.FromResult(new List<S3Object>());
        return Task.FromResult(new[] { "Olena", "Waiting_Person", "Broken", "Denied", "Failed" }
            .Select((name, i) => new S3Object
            {
                Key = $"resume/Backend/{name}_2026-10-05_0123456789abcdef0123456789abcdef.pdf",
                LastModified = DateTime.UtcNow.AddMinutes(-i)
            }).Concat(new[] { new S3Object { Key = "resume/Backend/" }, new S3Object { Key = "resume/other.txt" } }).ToList());
    }
    public Task<string?> ReadResultAsync(string key)
    {
        ReadKeys.Add(key);
        if (key.Contains("Denied")) throw new AmazonS3Exception("Denied") { StatusCode = HttpStatusCode.Forbidden };
        return Task.FromResult<string?>(key.Contains("Waiting") ? null : key.Contains("Broken") ? "broken JSON"
            : key.Contains("Failed") ? "{\"status\":\"failed\"}" : result);
    }
}
