using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using timelapse.api.Helpers;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    // Stores/serves the ESP32 units' SD-card logs (see uploadPendingLogs() in main.cpp) and
    // extracted post-crash core dumps (see checkAndLogCoreDump()/uploadPendingCoreDumps()) -
    // added so a unit's recent activity, and any panic backtrace, can be read from the server
    // without needing physical SD-card access. Blob storage only, no DB table: the blob container
    // is the authoritative list of what's available, listed via prefix in GetDates()/GetCoreDumps().
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LogController{

        public LogController(AppDbContext appDbContext, ILogger<LogController> logger, IConfiguration configuration, IMemoryCache memoryCache){
            _appDbContext = appDbContext;
            _logger = logger;
            _storageHelper = new StorageHelper(configuration, logger, memoryCache);
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;
        private StorageHelper _storageHelper;

        private static string LogBlobName(int deviceId, string date) => $"logs/{deviceId}/{date}.log";
        private static string CoreDumpBlobPrefix(int deviceId) => $"logs/{deviceId}/coredumps/";

        // ESP32 devices push here every wake cycle, identified by SerialNumber - no user login
        // involved, so this stays open regardless of the class-level [Authorize] above. Appends
        // rather than replaces (see StorageHelper.AppendText()) - the device only ever sends the
        // tail it hasn't sent yet (see uploadPendingLogs()), so this just needs to land at the end
        // of whatever's already there for that device/date.
        [AllowAnonymous]
        [HttpPost]
        public ActionResult Post([FromForm] LogPostModel model){
            Device device = _appDbContext.Devices.FirstOrDefault(d => d.SerialNumber == model.SerialNumber);
            if(device==null){
                return new NotFoundResult();
            }

            using(var reader = new StreamReader(model.File.OpenReadStream())){
                string text = reader.ReadToEnd();
                _storageHelper.AppendText(LogBlobName(device.Id, model.Date), text);
            }

            return new OkResult();
        }

        // One-shot upload of a single extracted core dump - see checkAndLogCoreDump() in the
        // firmware. Unlike the log text above, each crash produces its own separate blob (named
        // after the device's own filename, which already carries a millis()-based disambiguator)
        // rather than being appended to anything.
        [AllowAnonymous]
        [HttpPost("CoreDump")]
        public ActionResult PostCoreDump([FromForm] CoreDumpPostModel model){
            Device device = _appDbContext.Devices.FirstOrDefault(d => d.SerialNumber == model.SerialNumber);
            if(device==null){
                return new NotFoundResult();
            }

            string blobName = CoreDumpBlobPrefix(device.Id) + model.File.FileName;
            _storageHelper.Upload(blobName, model.File.OpenReadStream());

            return new OkResult();
        }

        // Returns one day's log as plain text - open it straight in a browser, or `curl` it.
        [HttpGet]
        public ActionResult GetLog([FromQuery] int deviceId, [FromQuery] string date){
            byte[] content = _storageHelper.DownloadBytes(LogBlobName(deviceId, date));
            if(content==null){
                return new NotFoundResult();
            }
            return new FileContentResult(content, "text/plain");
        }

        // Which calendar days have a log available for this device, most recent first - drives
        // whatever picks a date to pass to GetLog() above.
        [HttpGet("Dates")]
        public ActionResult<IEnumerable<string>> GetDates([FromQuery] int deviceId){
            string prefix = $"logs/{deviceId}/";
            var dates = _storageHelper.ListBlobNames(prefix)
                .Where(name => name.EndsWith(".log") && !name.Substring(prefix.Length).Contains('/'))
                .Select(name => name.Substring(prefix.Length, name.Length - prefix.Length - ".log".Length))
                .OrderByDescending(date => date)
                .ToList();

            return dates;
        }

        // Which core dumps are available for this device - each entry is a filename to pass to
        // GetCoreDump() below. Most recent first (filenames are millis()-since-boot, not sortable
        // across boots, but blob listing is already alphabetical/chronological-enough in practice
        // since it's one device crashing occasionally, not a firehose).
        [HttpGet("CoreDump")]
        public ActionResult<IEnumerable<string>> GetCoreDumps([FromQuery] int deviceId){
            string prefix = CoreDumpBlobPrefix(deviceId);
            var names = _storageHelper.ListBlobNames(prefix)
                .Select(name => name.Substring(prefix.Length))
                .OrderDescending()
                .ToList();

            return names;
        }

        // Downloads one core dump's raw bytes (ELF-format - see checkAndLogCoreDump()) - feed
        // straight to `espcoredump.py info_corefile -t elf --core <file> <matching firmware.elf>`
        // (the elf_sha256 logged alongside the summary in the device's own log confirms which
        // build's firmware.elf to use) for a full backtrace with locals, not just the addresses in
        // the summary line.
        [HttpGet("CoreDump/{fileName}")]
        public ActionResult GetCoreDump([FromQuery] int deviceId, string fileName){
            byte[] content = _storageHelper.DownloadBytes(CoreDumpBlobPrefix(deviceId) + fileName);
            if(content==null){
                return new NotFoundResult();
            }
            return new FileContentResult(content, "application/octet-stream"){
                FileDownloadName = fileName
            };
        }
    }
}
