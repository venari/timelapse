namespace timelapse.api{

    public class CreateEventRequest
    {
        public int ImageId {get; set;}
        public DateTime StartTime {get; set;}
        public DateTime EndTime {get; set;}
        public string Description {get; set;}
        public List<int> EventTypeIds {get; set;} = new();
    }

    public class UpdateEventRequest
    {
        public DateTime StartTime {get; set;}
        public DateTime EndTime {get; set;}
        public string Description {get; set;}
        public List<int> EventTypeIds {get; set;} = new();
    }
}
