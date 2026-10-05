using System.ComponentModel.DataAnnotations;


namespace timelapse.core.models;

public class ImageSubset{
    public int Id {get; set;}
    public DateTime Timestamp {get; set;}
    public Uri BlobUri {get; set;}
}

public class Image
{
    public int Id {get; set;}
    [Required]
    public DateTime Timestamp {get; set;}
    // [Required]
    // public IFormFile file {get; set;}
    public Uri BlobUri {get; set;}

    [Required]
    public int DeviceId {get; set;}
    public Device Device {get; set;}
}