using System.Threading;
using Microsoft.Build.Utilities;

namespace DjvuNet.Build.Tasks
{
    public static class TaskLogger
    {
        private static readonly AsyncLocal<TaskLoggingHelper> _current = new AsyncLocal<TaskLoggingHelper>();
        public static TaskLoggingHelper Current 
        { 
            get => _current.Value; 
            set => _current.Value = value; 
        }
    }
}