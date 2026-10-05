using System.Text.Json;
using System.Text.RegularExpressions;
using MyHRExample.Models;

namespace MyHRExample.Service;

public class CandidateAnalysisService(
    IAnalysisStorage storage, IConfiguration configuration, ILogger<CandidateAnalysisService> logger)
{
    private string ResultsPrefix => (configuration["Analysis:ResultsPrefix"] ?? "results").Trim('/') + "/";

    public async Task<List<CandidateAnalysisViewModel>> GetCandidatesAsync(string? onlyKey = null)
    {
        var resumes = await storage.GetFilesAsync("resume/");
        var candidates = resumes
            .Where(file => IsResume(file.Key) && (onlyKey == null || file.Key == onlyKey))
            .OrderByDescending(file => file.LastModified)
            .Select(file => new CandidateAnalysisViewModel
            {
                ResumeKey = file.Key,
                Name = Regex.Replace(Path.GetFileNameWithoutExtension(file.Key),
                    @"_\d{4}-\d{2}-\d{2}_[a-fA-F0-9]{32}$", "").Replace('_', ' '),
                Vacancy = file.Key["resume/".Length..].Contains('/')
                    ? file.Key["resume/".Length..file.Key.LastIndexOf('/')] : "Без зазначеної вакансії",
                UploadedAt = file.LastModified
            }).ToList();
        await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = 6 },
            async (candidate, _) => await LoadResultAsync(candidate));
        return candidates;
    }

    private static bool IsResume(string key) => key.StartsWith("resume/", StringComparison.Ordinal)
        && new[] { ".pdf", ".doc", ".docx" }.Contains(Path.GetExtension(key).ToLowerInvariant());

    private async Task LoadResultAsync(CandidateAnalysisViewModel candidate)
    {
        var relativeKey = candidate.ResumeKey["resume/".Length..];
        var key = ResultsPrefix + relativeKey[..^Path.GetExtension(relativeKey).Length] + ".json";
        try
        {
            var json = await storage.ReadResultAsync(key);
            if (json == null) return;
            ApplyResult(candidate, json);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid candidate analysis in {Key}", key);
            candidate.Status = AnalysisStatus.Failed;
            candidate.Notice = "Файл результату має некоректний формат. Перевірте JSON, створений Lambda.";
        }
        catch (Exception exception) when (exception is Amazon.Runtime.AmazonServiceException
            or Amazon.Runtime.AmazonClientException or S3ConfigurationException or IOException)
        {
            logger.LogWarning(exception, "Cannot read candidate analysis {Key}", key);
            candidate.Status = AnalysisStatus.Unavailable;
            candidate.Notice = "Не вдалося прочитати результат із S3. Спробуйте оновити сторінку пізніше.";
        }
    }

    public static void ApplyResult(CandidateAnalysisViewModel candidate, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected an object.");
        candidate.FullJson = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        var status = Text(root, "status")?.ToLowerInvariant();
        if (status is "pending" or "processing") { candidate.Status = AnalysisStatus.Pending; return; }
        if (status is "failed" or "error")
        {
            candidate.Status = AnalysisStatus.Failed;
            candidate.Notice = "Lambda повідомила про помилку аналізу.";
            return;
        }
        if (status != null && status is not ("completed" or "success")) throw new JsonException("Unknown status.");
        var skills = Items(root, "skills");
        var confirmed = Items(root, "confirmedRequirements");
        var missing = Items(root, "missingInformation");
        var summary = Text(root, "summary") ?? throw new JsonException("Missing summary.");
        candidate.Name = Text(root, "candidateName") is { Length: > 0 } name ? name : candidate.Name;
        candidate.Skills = skills;
        candidate.ConfirmedRequirements = confirmed;
        candidate.MissingInformation = missing;
        candidate.Summary = summary;
        candidate.Status = AnalysisStatus.Completed;
    }

    private static JsonElement? Property(JsonElement root, string name) => root.EnumerateObject()
        .Where(p => p.Name.Replace("_", "").Equals(name, StringComparison.OrdinalIgnoreCase))
        .Select(p => (JsonElement?)p.Value).FirstOrDefault();

    private static string? Text(JsonElement root, string name)
    {
        var value = Property(root, name);
        if (value == null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (value.Value.ValueKind != JsonValueKind.String) throw new JsonException($"{name} must be a string.");
        return value.Value.GetString();
    }

    private static List<string> Items(JsonElement root, string name)
    {
        var value = Property(root, name);
        if (value == null || value.Value.ValueKind != JsonValueKind.Array) throw new JsonException($"{name} must be an array.");
        return value.Value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString()! : throw new JsonException($"{name} must contain strings.")).ToList();
    }
}
