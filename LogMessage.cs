using System.ComponentModel;
using System.Threading;

namespace _6112020_SunnenSafetyParameterEALTest
{
    public class LogMessage
    {
        private BackgroundWorker _backgroundWorker;
        SynchronizationContext _context;
        public string LogEvent { get; set; }
        public string LogEventDescription { get; set; }
        public System.DateTime LogTime { get; set; }
    }
}