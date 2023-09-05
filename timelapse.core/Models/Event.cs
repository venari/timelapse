using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class EventType{
    public int Id {get; set;}
    [Required]
    public string Name {get; set;}
    public string Description {get; set;}

    public List<Event> Events {get;} = new();

}

public class Event
{
    public int Id {get; set;}
    [Required]
    public DateTime StartTime {get; set;}

    [Required]
    public DateTime EndTime {get; set;}

    [Required]
    public DateTime CreatedDate {get; set;} = DateTime.UtcNow;
    [Required]
    public string CreatedByUserId {get; set;}

    [Required]
    public DateTime LastEditedDate {get; set;} = DateTime.UtcNow;
    [Required]
    public string LastEditedByUserId {get; set;}

    public string Description {get; set;}

    [Required]
    public int DeviceId {get; set;}
    public Device Device {get; set;}

    public List<EventComment> Comments {get; set;} = new List<EventComment>();

    public int EventTypeId {get; set;}
    public EventType EventType {get; set;}

    // public List<int> EventTypeIds {
    //     get{
    //         return EventTypes.Select(et => et.Id).ToList();
    //         // var eventTypeIds = new List<int>();
    //         // foreach(var et in EventTypes){
    //         //     eventTypeIds.Add(et.Id);
    //         // }

    //         // return EventTypeIds;
    //     }
    //     set{


    //     }
    // }
    public List<EventType> EventTypes {get;} = new();

    public int StartImageId {get; set;}
    public Image StartImage {get; set;}

    // public int ThumbnailImageId {get; set;}
    // public Image ThumbnailImage {get; set;}

    public int EndImageId {get; set;}
    public Image EndImage {get; set;}
}