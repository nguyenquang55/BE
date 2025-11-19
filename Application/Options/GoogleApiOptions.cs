using System;

namespace Application.Options
{
    public class GoogleApiOptions
    {
        public CalendarOptions Calendar { get; set; } = new CalendarOptions();
        public GmailOptions Gmail { get; set; } = new GmailOptions();

        public TimeSpan CalendarCacheLifetime => TimeSpan.FromMinutes(Math.Max(1, Calendar.TitleCacheMinutes));
        public TimeSpan GmailCacheLifetime => TimeSpan.FromMinutes(Math.Max(1, Gmail.TitleCacheMinutes));

        public sealed class CalendarOptions
        {
            public string BaseUrl { get; set; } = "https://www.googleapis.com/calendar/v3";
            public string DefaultCalendarId { get; set; } = "primary";
            public int TitleCacheMinutes { get; set; } = 30;
            public int MaxSearchResults { get; set; } = 5;
        }

        public sealed class GmailOptions
        {
            public string BaseUrl { get; set; } = "https://gmail.googleapis.com/gmail/v1";
            public string UserAlias { get; set; } = "me";
            public int TitleCacheMinutes { get; set; } = 30;
            public int MaxSearchResults { get; set; } = 5;
        }
    }
}
