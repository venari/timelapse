using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    // Matches DeviceEdit.cshtml's own [Authorize] - this URL+key was previously only
    // ever rendered into that logged-in-only page's HTML.
    [Authorize]
    public class ConfigController{

        public ConfigController(IConfiguration configuration){
            _configuration = configuration;
        }

        private IConfiguration _configuration;

        // Same LINZ basemap URL (with API key substituted in) that DeviceEdit.cshtml's
        // PageModel constructor already exposes server-side into the rendered HTML/JS -
        // this just delivers it to the React app instead.
        [HttpGet("Basemap")]
        public ActionResult<object> GetBasemap(){
            string template = _configuration["LINZ-Aerial-Imagery-Basemap-XYZ-Template"];
            string apiKey = _configuration["LINZApiKey"];
            string url = template.Replace("<LINZ-api-key>", apiKey);

            return new { url };
        }
    }
}
