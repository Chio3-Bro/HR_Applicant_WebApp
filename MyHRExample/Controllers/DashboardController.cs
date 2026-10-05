using Microsoft.AspNetCore.Mvc;
using MyHRExample.Models;
using MyHRExample.Service;

namespace MyHRExample.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DashboardController(CandidateAnalysisService analysis, ILogger<DashboardController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel();
        try { model.Candidates = await analysis.GetCandidatesAsync(); }
        catch (Exception exception) when (IsStorageError(exception))
        {
            logger.LogWarning(exception, "Cannot load HR dashboard");
            model.Error = "Не вдалося завантажити кандидатів. Перевірте налаштування AWS та доступ до S3 і повторіть спробу.";
        }
        return View("Dashboard", model);
    }

    public async Task<IActionResult> Details(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return NotFound();
        try
        {
            var candidate = (await analysis.GetCandidatesAsync(key)).SingleOrDefault();
            return candidate == null ? NotFound() : View(candidate);
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            logger.LogWarning(exception, "Cannot load candidate details");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Dashboard", new DashboardViewModel
            { Error = "Не вдалося завантажити кандидата з S3. Повторіть спробу пізніше." });
        }
    }

    private static bool IsStorageError(Exception exception) => exception is
        Amazon.Runtime.AmazonServiceException or Amazon.Runtime.AmazonClientException
        or S3ConfigurationException or IOException;
}
