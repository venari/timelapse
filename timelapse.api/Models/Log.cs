using System.ComponentModel.DataAnnotations;

namespace timelapse.api{

    // A chunk of an ESP32 device's local log file, appended server-side rather than stored as a
    // DB row - see LogController.Post(). Offset is informational only (logged, not enforced
    // server-side): the device already guarantees it only ever sends its own unsent tail, and
    // append blobs have no concept of "write at this offset" to check it against anyway.
    public class LogPostModel
    {
        [Required]
        public string SerialNumber {get; set;}
        [Required]
        public string Date {get; set;}   // yyyy-MM-dd - the device's local calendar day this chunk belongs to
        public int? Offset {get; set;}
        [Required]
        public IFormFile File {get; set;}
    }

    // A core dump extracted from flash after a crash - see LogController.PostCoreDump() and the
    // ESP32 firmware's checkAndLogCoreDump(). One-shot upload (not appended) - each crash produces
    // a separate blob.
    public class CoreDumpPostModel
    {
        [Required]
        public string SerialNumber {get; set;}
        [Required]
        public IFormFile File {get; set;}
    }
}
