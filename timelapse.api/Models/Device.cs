using System.ComponentModel.DataAnnotations;

namespace timelapse.api{

    public class DevicePostModel
    {
        [Required]
        public string Name {get; set;}
        public string Description {get; set;}
    }
}