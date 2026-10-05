using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Yarp.Sample.Ntlm.LegacyApp.Data;

namespace Yarp.Sample.Ntlm.LegacyApp.Pages.Cases;

public class DetailsModel : PageModel
{
    public MunicipalCase Case { get; private set; } = default!;

    public IActionResult OnGet(string number)
    {
        var found = CaseRegistry.Find(number);
        if (found is null)
        {
            return NotFound();
        }

        Case = found;
        return Page();
    }
}
