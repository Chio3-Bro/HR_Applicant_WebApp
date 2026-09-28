using Microsoft.AspNetCore.Mvc;
using MyHRExample.Models;
using MyHRExample.Service;

namespace MyHRExample.Controllers
{
    public class ApplicantController : Controller
    {
        private readonly S3Service _s3Service;

        public ApplicantController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }


        // ==========================================
        // SHOW APPLICANT PAGE
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Applicant()
        {
            var model = new ApplicantViewModel();

            try
            {
                // Get only files from criteria/
                var files =
                    await _s3Service.GetFilesAsync("criteria/");

                foreach (var file in files)
                {
                    // Ignore folder entries
                    if (file.Key.EndsWith("/"))
                        continue;

                    // Read the content of the criteria file
                    var content =
                        await _s3Service.GetFileContentAsync(
                            file.Key);

                    // Example:
                    // criteria/abc123.txt
                    // becomes:
                    // abc123
                    var vacancyName =
                        Path.GetFileNameWithoutExtension(
                            file.Key);

                    model.Vacancies.Add(
                        new VacancyViewModel
                        {
                            Key = file.Key,
                            Name = vacancyName,
                            Criteria = content
                        });
                }
            }
            catch (S3ConfigurationException exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    exception.Message);
            }

            return View(model);
        }


        // ==========================================
        // UPLOAD RESUME
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadResume(
            string selectedCriteria,
            string name,
            IFormFile? file)
        {
            // --------------------------------------
            // 1. Validate vacancy
            // --------------------------------------

            if (string.IsNullOrWhiteSpace(selectedCriteria))
            {
                TempData["ErrorMessage"] =
                    "Please select a vacancy.";

                return RedirectToAction(nameof(Applicant));
            }


            // --------------------------------------
            // 2. Validate applicant name
            // --------------------------------------

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] =
                    "Please enter your name.";

                return RedirectToAction(nameof(Applicant));
            }


            // --------------------------------------
            // 3. Validate file
            // --------------------------------------

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a résumé.";

                return RedirectToAction(nameof(Applicant));
            }


            // Maximum file size: 10 MB
            if (file.Length > 10 * 1024 * 1024)
            {
                TempData["ErrorMessage"] =
                    "The file must be 10 MB or smaller.";

                return RedirectToAction(nameof(Applicant));
            }


            // --------------------------------------
            // 4. Validate file extension
            // --------------------------------------

            var extension =
                Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

            var allowedExtensions =
                new[] { ".pdf", ".doc", ".docx" };

            if (!allowedExtensions.Contains(extension))
            {
                TempData["ErrorMessage"] =
                    "Only PDF, DOC and DOCX files are allowed.";

                return RedirectToAction(nameof(Applicant));
            }


            // --------------------------------------
            // 5. Get vacancy name
            // --------------------------------------

            // Example:
            //
            // criteria/abc123.txt
            //
            // becomes:
            //
            // abc123

            var vacancyName =
                Path.GetFileNameWithoutExtension(
                    selectedCriteria);


            // --------------------------------------
            // 6. Clean applicant name
            // --------------------------------------

            // Example:
            //
            // John Smith
            //
            // becomes:
            //
            // John_Smith

            var cleanName = name.Trim();

            foreach (var invalidChar
                     in Path.GetInvalidFileNameChars())
            {
                cleanName =
                    cleanName.Replace(
                        invalidChar.ToString(),
                        "");
            }

            // Replace spaces with underscores
            cleanName =
                cleanName.Replace(" ", "_");


            // --------------------------------------
            // 7. Create upload date
            // --------------------------------------

            // Example:
            // 2026-09-28

            var uploadDate =
                DateTime.Now.ToString("yyyy-MM-dd");


            // --------------------------------------
            // 8. Create unique GUID
            // --------------------------------------

            var guid =
                Guid.NewGuid().ToString("N");


            // --------------------------------------
            // 9. Create new filename
            // --------------------------------------

            // Example:
            //
            // John_Smith_2026-09-28_
            // 54c6204e987f4c858dcba34f02134d51.pdf

            var newFileName =
                $"{cleanName}_{uploadDate}_{guid}{extension}";


            // --------------------------------------
            // 10. Create S3 object key
            // --------------------------------------

            // Example:
            //
            // resume/
            //   abc123/
            //     John_Smith_2026-09-28_GUID.pdf

            var key =
                $"resume/{vacancyName}/{newFileName}";


            // --------------------------------------
            // 11. Upload résumé to S3
            // --------------------------------------

            using var stream =
                file.OpenReadStream();

            try
            {
                await _s3Service.UploadFileAsync(
                    stream,
                    key);
            }
            catch (S3ConfigurationException exception)
            {
                TempData["ErrorMessage"] =
                    exception.Message;

                return RedirectToAction(
                    nameof(Applicant));
            }


            // --------------------------------------
            // 12. Success
            // --------------------------------------

            TempData["UploadMessage"] =
                $"Thank you, {name.Trim()}. " +
                "Your résumé was successfully uploaded.";


            return RedirectToAction(
                nameof(Applicant));
        }
    }
}