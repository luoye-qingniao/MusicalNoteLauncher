using System.Windows.Media;

namespace MusicalNoteLauncher.Models
{
    public class LogEntry
    {
        public string Message { get; set; }
        public Brush Foreground { get; set; }
        public bool IsError { get; set; }

        public LogEntry(string message, bool isError = false)
        {
            Message = message;
            IsError = isError;
            Foreground = isError 
                ? new SolidColorBrush(Color.FromRgb(231, 76, 60)) 
                : new SolidColorBrush(Color.FromRgb(46, 204, 113));
        }
    }
}
