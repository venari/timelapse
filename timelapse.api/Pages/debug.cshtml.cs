using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace timelapse.api.Pages
{
    [Authorize(Roles="Admin")]
    public class debugModel : PageModel
    {
        public ActionResult OnGet()
        {
            return Page();
        }
    }
}
