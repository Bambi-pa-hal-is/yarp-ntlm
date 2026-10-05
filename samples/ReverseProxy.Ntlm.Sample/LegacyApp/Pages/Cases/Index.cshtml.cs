using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Yarp.Sample.Ntlm.LegacyApp.Data;

namespace Yarp.Sample.Ntlm.LegacyApp.Pages.Cases;

public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Department { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    public IReadOnlyList<MunicipalCase> Results { get; private set; } = [];

    public void OnGet()
    {
        Results = CaseRegistry.All
            .Where(c => string.IsNullOrEmpty(Department) || c.Department == Department)
            .Where(c => string.IsNullOrEmpty(Status) || c.Status == Status)
            .Where(c => string.IsNullOrEmpty(Query)
                || c.Title.Contains(Query, StringComparison.OrdinalIgnoreCase)
                || c.Number.Contains(Query, StringComparison.OrdinalIgnoreCase)
                || c.Applicant.Contains(Query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Received)
            .ToList();
    }
}
