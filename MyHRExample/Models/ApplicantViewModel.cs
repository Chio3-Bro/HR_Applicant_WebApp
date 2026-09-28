namespace MyHRExample.Models
{
    public class ApplicantViewModel
    {
        public List<VacancyViewModel> Vacancies { get; set; }
            = new List<VacancyViewModel>();
    }

    public class VacancyViewModel
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Criteria { get; set; } = "";
    }
}