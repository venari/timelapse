using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Filters;

namespace timelapse.api.Pages;

[Authorize]
[AllowAnonymous]
public class ImageViewModel : PageModel
{
    private readonly ILogger<ImageViewModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public Device device {get; private set;}
    // public string SasToken {get; private set;}

    // public DateTime oldestImageTimestamp {get; private set;}
    // public DateTime newestImageTimestamp {get; private set;}
    public ImageSubset[] imagesToShow {get; private set;}
    public DateTime SeekTo {get; private set;}
    public int imageToSeekTo {get; private set;}

    public ImageViewModel(ILogger<ImageViewModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        NumberOfHoursToDisplay = 24;
        _storageHelper = new StorageHelper(configuration, appDbContext, logger, memoryCache);
    }

    [BindProperty]
    public int NumberOfHoursToDisplay {get; set; }

    public IActionResult OnGet(int id, int? numberOfHoursToDisplay = null, DateTime? seekTo = null)
    {
        // Unusual authentication here - want to accept logged in users, and the Third Party key
        // Duplication of logic in ThirdPartyApiKeyAuthAttribute

        const string ApiKeyHeaderName = "api-key";
        if(HttpContext.User==null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated)
        {
            if (!HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var potentialApiKey))
            {
                // Not in headers? Let's try in query string?
                if(!HttpContext.Request.Query.TryGetValue(ApiKeyHeaderName, out potentialApiKey))
                {
                    return Redirect("/Identity/Account/Login");
                    // return Unauthorized();
                    // return new UnauthorizedResult();
                }
            }

            var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = configuration.GetValue<string>("ThirdParty_ApiKey");

            if(apiKey==null){
                return Redirect("/Identity/Account/Login");
            }

            if(!apiKey.Equals(potentialApiKey))
            {
                return Redirect("/Identity/Account/Login");
            }
        }

        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        DateTime? showFrom = null;
        DateTime? showTo = null;

        if(seekTo==null){

            showTo = _appDbContext.Images
                .Where(t => t.DeviceId == id)
                .OrderByDescending(t => t.Timestamp)
                .Select(t => t.Timestamp)
                .FirstOrDefault();

        } else {
            showTo = _appDbContext.Images
                .Where(t => t.DeviceId == id && t.Timestamp <= seekTo.Value.AddHours(0.5 * NumberOfHoursToDisplay))
                .OrderByDescending(t => t.Timestamp)
                .Select(t => t.Timestamp)
                .FirstOrDefault();

        }

        if(showTo==null || showTo == DateTime.MinValue){
            return RedirectToPage("/NotFound");
        }

        if(showTo.HasValue){
            showFrom = showTo.Value.AddHours(-1 * NumberOfHoursToDisplay);
        }

        var d = _appDbContext.Devices
            .Include(d => d.Images.Where(i =>i.Timestamp >= showFrom && i.Timestamp <= showTo))
            .FirstOrDefault(d => d.Id == id);

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        imagesToShow = d.Images.Select(i => new ImageSubset{Id = i.Id, Timestamp = i.Timestamp, BlobUri = i.BlobUri}).OrderBy(i => i.Timestamp).ToArray();
        if(imagesToShow.Count()==0){
            return RedirectToPage("/NotFound");
        }

        if(seekTo!=null){
            // imageToSeekTo = d.Images.Where(i =>i.Timestamp >= showFrom && i.Timestamp <= showTo).OrderBy(i => i.Timestamp).Select(i => i.Id).FirstOrDefault();
            imageToSeekTo = d.Images.Where(i => i.Timestamp < seekTo).Count();
            if(imageToSeekTo<0){
                imageToSeekTo = 0;
            }
            if(imageToSeekTo>=imagesToShow.Count()){
                imageToSeekTo = imagesToShow.Count()-1;
            }
            SeekTo = imagesToShow[imageToSeekTo].Timestamp;
            // imageToSeekTo = d.Images.Count/2;
        } else {
            imageToSeekTo = d.Images.Count() - 1;
            SeekTo = imagesToShow.Last().Timestamp;
        }

        // SasToken = _storageHelper.SasToken(imagesToShow[0].Id);


        return Page();
    }

    // public string GetSasTokenForImage(int imageId) {
    //     return _storageHelper.GetSasTokenForImage(imageId);
    // }
    public string GetSasTokenForImage(int imageIndex) {
        return _storageHelper.GetSasTokenForImage(imagesToShow[imageIndex].Id);
    }

}