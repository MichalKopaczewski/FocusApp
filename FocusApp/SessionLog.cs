using System;
using System.Collections.Generic;

namespace FocusApp
{
    public class SessionLog
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan WorkDuration { get; set; }
        public TimeSpan RestDuration { get; set; }
        public bool StoppedManually { get; set; }
        public string Mode { get; set; }
        public List<Distraction> Distractions { get; set; }
    }
}
