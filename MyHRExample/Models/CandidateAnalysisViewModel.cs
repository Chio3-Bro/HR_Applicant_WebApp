namespace MyHRExample.Models;

public enum AnalysisStatus { Pending, Completed, Failed, Unavailable }

public class CandidateAnalysisViewModel
{
    public string ResumeKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Vacancy { get; set; } = "";
    public DateTime? UploadedAt { get; set; }
    public AnalysisStatus Status { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<string> ConfirmedRequirements { get; set; } = [];
    public List<string> MissingInformation { get; set; } = [];
    public string Summary { get; set; } = "";
    public string? FullJson { get; set; }
    public string? Notice { get; set; }
    public string StatusText => Status switch
    {
        AnalysisStatus.Completed => "Аналіз завершено",
        AnalysisStatus.Failed => "Помилка аналізу",
        AnalysisStatus.Unavailable => "Результат недоступний",
        _ => "Очікує аналізу"
    };
    public string StatusClass => Status.ToString().ToLowerInvariant();
}

public class DashboardViewModel
{
    public List<CandidateAnalysisViewModel> Candidates { get; set; } = [];
    public string? Error { get; set; }
}
