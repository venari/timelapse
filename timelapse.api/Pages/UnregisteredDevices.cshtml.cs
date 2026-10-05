using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;

namespace timelapse.api.Pages;

[Authorize]
public class UnregisteredDevicesModel : PageModel
{
    private readonly ILogger<UnregisteredDevicesModel> _logger;
    private AppDbContext _appDbContext;

    public List<UnregisteredDevice> unregisteredDevices {get;}
    public string SasToken {get; private set;}

    public UnregisteredDevicesModel(ILogger<UnregisteredDevicesModel> logger, AppDbContext appDbContext, IConfiguration configuration)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        unregisteredDevices = _appDbContext.UnregisteredDevices
            .ToList();
    }

    public void OnGet()
    {

    }

    public Device Register(string serialNumber)
    {
        _logger.LogInformation($"Register device with serial number {serialNumber}");

        return null;
    }

}