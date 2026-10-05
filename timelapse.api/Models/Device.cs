using System.ComponentModel.DataAnnotations;

namespace timelapse.api{

    public class DevicePostModel
    {
        [Required]
        public string Name {get; set;}
        [Required]
        public string SerialNumber {get; set;}
        public string ShortDescription {get; set;}
        public string Description {get; set;}
    }
}