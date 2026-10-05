using System.ComponentModel.DataAnnotations;


namespace timelapse.core.models;

public abstract class Comment{
    public int Id {get; set;}
    public DateTime CreatedDate {get; set;} = DateTime.Now;
    public string UserId {get; set;}

    public string Text {get; set;}
}

public class EventComment : Comment{
    public Event Event {get; set;}
    public int EventId {get; set;}
}

