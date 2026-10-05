using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Yarp.Sample.Ntlm.LegacyApp.Data;

namespace Yarp.Sample.Ntlm.LegacyApp.Pages.Residents;

// Requires the CaseOfficer role, see AuthorizeFolder("/Residents") in Program.cs.
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    public IReadOnlyList<Resident>? Results { get; private set; }

    public void OnGet()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        _logger.LogInformation("Resident register search by {User}: {Query}", User.Identity?.Name, Query);

        Results = ResidentRegistry.All
            .Where(r => r.Name.Contains(Query, StringComparison.OrdinalIgnoreCase)
                || r.Address.Contains(Query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
