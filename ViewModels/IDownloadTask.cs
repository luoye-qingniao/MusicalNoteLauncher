using System.ComponentModel;

namespace MusicalNoteLauncher.ViewModels
{
    public interface IDownloadTask : INotifyPropertyChanged
    {
        string VersionId { get; }
        string Status { get; set; }
        double Progress { get; set; }
        long DownloadedBytes { get; set; }
        long TotalBytes { get; set; }
        string CurrentFile { get; set; }
        string DownloadSpeed { get; set; }
        string RemainingTime { get; set; }
        bool IsCompleted { get; set; }
        bool IsFailed { get; set; }
        bool IsCancelled { get; set; }
        bool IsPaused { get; set; }
        string ErrorMessage { get; set; }
        bool IsDownloading { get; }
        
        void Cancel();
        void Pause();
        string ProgressText { get; }
        string FileName { get; }
        string FileSize { get; }
        string Speed { get; }
        string StatusIcon { get; }
        string SizeText { get; }
    }
}
