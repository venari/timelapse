using System.ComponentModel.DataAnnotations;

namespace timelapse.api{

    public class ImagePostModel
    {
        [Required]
        public string SerialNumber {get; set;}
        public DateTime? Timestamp {get; set;}
        [Required]
        public IFormFile File {get; set;}
    }
}