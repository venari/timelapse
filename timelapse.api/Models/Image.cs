using System.ComponentModel.DataAnnotations;

namespace timelapse.api{

    public class ImagePostModel
    {
        [Required]
        public int DeviceId {get; set;}
        public DateTime Timestamp {get; set;}
        [Required]
        public IFormFile File {get; set;}
    }
}