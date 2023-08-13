using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using timelapse.api.Filters;
using timelapse.api.Helpers;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    public class EventController{

        public EventController(AppDbContext appDbContext, ILogger<EventController> logger){ //}, IConfiguration configuration, IMemoryCache memoryCache){
            _appDbContext = appDbContext;
            _logger = logger;
            // _storageHelper = new StorageHelper(configuration, logger, memoryCache);
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;
        // private StorageHelper _storageHelper;
        
        [HttpDelete]
        public ActionResult<Event> Delete(int eventId){
            _logger.LogInformation($"Deleting Event {eventId}...");

            Event Event = _appDbContext.Events.FirstOrDefault(e => e.Id == eventId);

            if(Event==null){
                return new NotFoundResult();
            }

            _appDbContext.Events.Remove(Event);
            _appDbContext.SaveChanges();
            return Event;
        }
    }
}