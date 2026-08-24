using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Areas.Identity.Data;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EventController : ControllerBase{

        public EventController(AppDbContext appDbContext, ILogger<EventController> logger){
            _appDbContext = appDbContext;
            _logger = logger;
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;

        [HttpGet]
        public ActionResult<IEnumerable<object>> GetEvents([FromQuery] int days = 7){
            DateTime cutOff = DateTime.UtcNow.AddDays(-1 * days);

            // Matches the clamp already in Events/Index.cshtml.cs.
            var floor = new DateTime(2024, 10, 01).ToUniversalTime();
            if(cutOff < floor){
                cutOff = floor;
            }

            var events = _appDbContext.Events
                .Include(e => e.Device)
                .Include(e => e.EventTypes)
                .Include(e => e.StartImage)
                .Include(e => e.EndImage)
                .Where(e => e.EndTime >= cutOff)
                .OrderByDescending(e => e.StartTime)
                .ToList();

            var userNames = GetUserNames(events.Select(e => e.CreatedByUserId));

            var result = events.Select(e => ToSummary(e, userNames));

            return result.ToList();
        }

        [HttpGet("Types")]
        public ActionResult<IEnumerable<EventType>> GetEventTypes(){
            return _appDbContext.EventTypes.OrderBy(et => et.Name).ToList();
        }

        [HttpGet("{id}")]
        public ActionResult<object> GetEvent(int id){
            var Event = _appDbContext.Events
                .Include(e => e.Device)
                .Include(e => e.EventTypes)
                .Include(e => e.StartImage)
                .Include(e => e.EndImage)
                .FirstOrDefault(e => e.Id == id);

            if(Event == null){
                return new NotFoundResult();
            }

            // Same +/- 5 minute window as Events/Detail.cshtml.cs, used to drive the
            // frame-by-frame scrubber on the event detail page.
            var eventImages = _appDbContext.Images
                .Where(i => i.DeviceId == Event.DeviceId
                    && i.Timestamp >= Event.StartTime.ToUniversalTime().AddMinutes(-5)
                    && i.Timestamp <= Event.EndTime.ToUniversalTime().AddMinutes(5))
                .OrderBy(i => i.Timestamp)
                .Select(i => new { i.Id, i.Timestamp, i.BlobUri })
                .ToList();

            var userNames = GetUserNames(new[] { Event.CreatedByUserId, Event.LastEditedByUserId });

            var result = new {
                Event.Id,
                Event.StartTime,
                Event.EndTime,
                Event.Description,
                Event.CreatedDate,
                Event.LastEditedDate,
                Device = new { Event.Device.Id, Event.Device.Name, Event.Device.Description },
                EventTypes = Event.EventTypes.OrderBy(et => et.Name).Select(et => new { et.Id, et.Name }),
                StartImage = Event.StartImage,
                EndImage = Event.EndImage,
                CreatedBy = userNames.GetValueOrDefault(Event.CreatedByUserId, "[unknown]"),
                LastEditedBy = userNames.GetValueOrDefault(Event.LastEditedByUserId, "[unknown]"),
                EventImages = eventImages
            };

            return result;
        }

        [HttpPost]
        public async Task<ActionResult<object>> CreateEvent([FromBody] CreateEventRequest request){
            var user = GetCurrentUser();
            if(user == null){
                return new UnauthorizedResult();
            }

            var image = _appDbContext.Images.FirstOrDefault(i => i.Id == request.ImageId);
            if(image == null){
                return new NotFoundResult();
            }

            var validationError = ValidateEventRequest(request.StartTime, request.EndTime, request.EventTypeIds);
            if(validationError != null){
                return new BadRequestObjectResult(new { error = validationError });
            }

            var newEvent = new Event();
            newEvent.LastEditedByUserId = newEvent.CreatedByUserId = user.Id;
            newEvent.DeviceId = image.DeviceId;
            newEvent.StartTime = request.StartTime;
            newEvent.EndTime = request.EndTime;
            newEvent.Description = request.Description;

            ApplyEventTypes(newEvent, request.EventTypeIds);
            ApplyStartAndEndImages(newEvent);

            _appDbContext.Events.Add(newEvent);
            await _appDbContext.SaveChangesAsync();

            return GetEvent(newEvent.Id);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<object>> UpdateEvent(int id, [FromBody] UpdateEventRequest request){
            var user = GetCurrentUser();
            if(user == null){
                return new UnauthorizedResult();
            }

            var existingEvent = _appDbContext.Events
                .Include(e => e.EventTypes)
                .FirstOrDefault(e => e.Id == id);

            if(existingEvent == null){
                return new NotFoundResult();
            }

            var validationError = ValidateEventRequest(request.StartTime, request.EndTime, request.EventTypeIds);
            if(validationError != null){
                return new BadRequestObjectResult(new { error = validationError });
            }

            existingEvent.LastEditedByUserId = user.Id;
            existingEvent.LastEditedDate = DateTime.UtcNow;
            existingEvent.StartTime = request.StartTime;
            existingEvent.EndTime = request.EndTime;
            existingEvent.Description = request.Description;

            existingEvent.EventTypes.Clear();
            ApplyEventTypes(existingEvent, request.EventTypeIds);
            ApplyStartAndEndImages(existingEvent);

            await _appDbContext.SaveChangesAsync();

            return GetEvent(existingEvent.Id);
        }

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

        private string? ValidateEventRequest(DateTime startTime, DateTime endTime, List<int> eventTypeIds){
            if(startTime > endTime){
                return "End Time is not later than Start Time.";
            }
            if(eventTypeIds == null || eventTypeIds.Count == 0){
                return "At least one Event Type must be selected.";
            }
            return null;
        }

        private void ApplyEventTypes(Event Event, List<int> eventTypeIds){
            foreach(var eventTypeId in eventTypeIds){
                var eventType = _appDbContext.EventTypes.FirstOrDefault(et => et.Id == eventTypeId);
                if(eventType != null){
                    Event.EventTypes.Add(eventType);
                }
            }
        }

        private void ApplyStartAndEndImages(Event Event){
            Event.StartImage = _appDbContext.Images.OrderBy(i => i.Timestamp).FirstOrDefault(i => i.DeviceId == Event.DeviceId && i.Timestamp >= Event.StartTime);
            Event.EndImage = _appDbContext.Images.OrderByDescending(i => i.Timestamp).FirstOrDefault(i => i.DeviceId == Event.DeviceId && i.Timestamp <= Event.EndTime);
        }

        private object ToSummary(Event Event, Dictionary<string, string> userNames){
            return new {
                Event.Id,
                Event.StartTime,
                Event.EndTime,
                Event.Description,
                Event.CreatedDate,
                Device = new { Event.Device.Id, Event.Device.Name, Event.Device.Description },
                EventTypes = Event.EventTypes.OrderBy(et => et.Name).Select(et => new { et.Id, et.Name }),
                StartImage = Event.StartImage,
                EndImage = Event.EndImage,
                CreatedBy = userNames.GetValueOrDefault(Event.CreatedByUserId, "[unknown]")
            };
        }

        private Dictionary<string, string> GetUserNames(IEnumerable<string> userIds){
            var ids = userIds.Distinct().ToList();
            return _appDbContext.Users
                .Where(u => ids.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u.UserName);
        }

        private AppUser? GetCurrentUser(){
            if(User?.Identity == null || !User.Identity.IsAuthenticated){
                return null;
            }
            return _appDbContext.Users.SingleOrDefault(u => u.UserName == User.Identity.Name);
        }
    }
}
