using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Yarp.Sample.Ntlm.LegacyApp.Pages;

[AllowAnonymous]
public class StatusModel : PageModel
{
    public string Heading { get; private set; } = "";

    public string Message { get; private set; } = "";

    public void OnGet(int code)
    {
        (Heading, Message) = code switch
        {
            403 => ("Access denied", "You don't have permission to view this page. Contact your manager to be added to the right AD group."),
            404 => ("Page not found", "The page or case you are looking for does not exist."),
            _ => ($"Error {code}", "Something went wrong. Contact IT support on ext. 4400."),
        };
    }
}
