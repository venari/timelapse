namespace timelapse.api.Helpers
{
    public static class DateTimeHelpers
    {
        public static DateTime RoundUpToNearest30(this DateTime datetime)
        {
            double atMinuteInBlock = datetime.TimeOfDay.TotalMinutes % 30;
            double minutesToAdd = 30 - atMinuteInBlock;
            return datetime.AddMinutes(minutesToAdd);
        }

        public static DateTime RoundDownToNearest30(this DateTime datetime)
        {
            double atMinuteInBlock = datetime.TimeOfDay.TotalMinutes % 30;
            return datetime.AddMinutes(-atMinuteInBlock);
        }

        public static DateTime RoundDownToNearestHour(this DateTime datetime)
        {
            // double atMinutes = datetime.TimeOfDay.Minutes;
            // double atSeconds = datetime.TimeOfDay.Seconds;
            // double atMilliseconds = datetime.TimeOfDay.Milliseconds;

            // return datetime.AddMinutes(-datetime.Minute).AddSeconds(-datetime.Second).AddMilliseconds(-datetime.Millisecond); //.AddMicroseconds(-datetime.Microsecond);
            return datetime.AddMinutes(-datetime.Minute).AddSeconds(-datetime.Second).AddTicks(-datetime.Ticks % TimeSpan.TicksPerSecond);
        }

        public static DateTime RoundToNearest30(this DateTime datetime)
        {
            double atMinuteInBlock = datetime.TimeOfDay.TotalMinutes % 30;
            if(atMinuteInBlock<15){
                return datetime.AddMinutes(-atMinuteInBlock);
            } else {
                double minutesToAdd = 30 - atMinuteInBlock;
                return datetime.AddMinutes(minutesToAdd);
            }
        }
    }
}