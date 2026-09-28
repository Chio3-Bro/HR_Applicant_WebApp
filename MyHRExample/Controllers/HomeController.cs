using Microsoft.AspNetCore.Mvc;
using MyHRExample.Models;
using MyHRExample.Service;
using System.Text;

namespace MyHRExample.Controllers
{
    public class HomeController : Controller
    {
        private readonly S3Service _s3Service;

        public HomeController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }


        [HttpGet]
        public IActionResult Index()
        {
            return View(
                new ApplicantCriteriaViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadCriteria(
            ApplicantCriteriaViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Index", model);


            using var stream =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        model.Criteria));


            var key =
                $"criteria/{Guid.NewGuid():N}.txt";


            try
            {
                await _s3Service.UploadFileAsync(
                    stream,
                    key);
            }
            catch (S3ConfigurationException exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    exception.Message);

                return View("Index", model);
            }


            TempData["UploadMessage"] =
                "Your criteria were uploaded to S3.";


            return RedirectToAction(
                nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResumeSave(
            IFormFile? file)
        {
            if (file is null || file.Length == 0)
                return BadRequest(
                    "Please select a non-empty file.");


            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(
                    "The file must be 10 MB or smaller.");


            var key =
                $"incoming/file-{Guid.NewGuid():N}";


            using Stream stream =
                file.OpenReadStream();


            try
            {
                await _s3Service.UploadFileAsync(
                    stream,
                    key);
            }
            catch (S3ConfigurationException exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    exception.Message);

                return View(
                    "Index",
                    new ApplicantCriteriaViewModel());
            }


            TempData["UploadMessage"] =
                "Your résumé was uploaded to S3.";


            return RedirectToAction(
                nameof(Index));
        }
    }
}