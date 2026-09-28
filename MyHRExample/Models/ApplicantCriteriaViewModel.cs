using System.ComponentModel.DataAnnotations;

namespace MyHRExample.Models
{
    public class ApplicantCriteriaViewModel
    {
        [Required(ErrorMessage = "Please describe your applicant criteria.")]
        public string Criteria { get; set; } = string.Empty;
        public bool IsSubmitted { get; set; }
    }
}
