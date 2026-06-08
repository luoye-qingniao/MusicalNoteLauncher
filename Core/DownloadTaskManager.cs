using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Core
{
    public class DownloadTaskManager : INotifyPropertyChanged
    {
        private static DownloadTaskManager _instance;
        public static DownloadTaskManager Instance => _instance ??= new DownloadTaskManager();

        private ObservableCollection<IDownloadTask> _tasks;
        private bool _isLoading;

        public ObservableCollection<IDownloadTask> Tasks
        {
            get => _tasks;
            private set
            {
                _tasks = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasTasks));
                OnPropertyChanged(nameof(ActiveTaskCount));
            }
        }

        public bool HasTasks => Tasks != null && Tasks.Count > 0;
        public int ActiveTaskCount => Tasks?.Count ?? 0;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<IDownloadTask> TaskAdded;
        public event Action<IDownloadTask> TaskCompleted;
        public event Action<IDownloadTask> TaskFailed;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DownloadTaskManager()
        {
            _tasks = new ObservableCollection<IDownloadTask>();
        }

        public void AddTask(IDownloadTask task)
        {
            if (task == null) return;

            if (IsTaskExists(task.VersionId))
            {
                Logger.Warning($"[下载任务] 任务已存在，跳过添加: {task.VersionId}");
                return;
            }

            Tasks.Add(task);
            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(ActiveTaskCount));

            task.PropertyChanged += OnTaskPropertyChanged;
            TaskAdded?.Invoke(task);

            Logger.Info($"[下载任务] 已添加任务: {task.VersionId}");
        }

        public bool IsTaskExists(string versionId)
        {
            return Tasks.Any(t => t.VersionId == versionId && !t.IsCompleted && !t.IsFailed && !t.IsCancelled);
        }

        public void RemoveTask(IDownloadTask task)
        {
            if (task == null) return;

            task.PropertyChanged -= OnTaskPropertyChanged;
            Tasks.Remove(task);
            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(ActiveTaskCount));

            Logger.Info($"[下载任务] 已移除任务: {task.VersionId}");
        }

        public void DeleteTask(IDownloadTask task)
        {
            if (task == null) return;

            try
            {
                task.Cancel();

                task.PropertyChanged -= OnTaskPropertyChanged;
                Tasks.Remove(task);
                OnPropertyChanged(nameof(HasTasks));
                OnPropertyChanged(nameof(ActiveTaskCount));

                Logger.Info($"[下载任务] 已删除任务: {task.VersionId}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[下载任务] 删除任务失败: {task.VersionId}, 错误: {ex.Message}");
            }
        }

        public void ResumeTask(DownloadTaskViewModel task)
        {
            if (task == null) return;

            try
            {
                _ = task.StartDownloadAsync();
                Logger.Info($"[下载任务] 已恢复任务: {task.VersionId}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[下载任务] 恢复任务失败: {task.VersionId}, 错误: {ex.Message}");
            }
        }

        public void ClearCompletedTasks()
        {
            var completedTasks = new List<IDownloadTask>();
            foreach (var task in Tasks)
            {
                if (task.IsCompleted || task.IsFailed || task.IsCancelled)
                {
                    completedTasks.Add(task);
                }
            }

            foreach (var task in completedTasks)
            {
                task.PropertyChanged -= OnTaskPropertyChanged;
                Tasks.Remove(task);
            }

            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(ActiveTaskCount));

            Logger.Info($"[下载任务] 已清空 {completedTasks.Count} 个已完成任务");
        }

        public void PauseAllTasks()
        {
            foreach (var task in Tasks)
            {
                if (task.IsDownloading)
                {
                    task.Pause();
                }
            }
            Logger.Info("[下载任务] 已暂停所有任务");
        }

        public void CancelAllTasks()
        {
            foreach (var task in Tasks)
            {
                if (task.IsDownloading)
                {
                    task.Cancel();
                }
            }
            Logger.Info("[下载任务] 已取消所有任务");
        }

        private void OnTaskPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is IDownloadTask task)
            {
                if (e.PropertyName == nameof(IDownloadTask.IsCompleted) && task.IsCompleted)
                {
                    TaskCompleted?.Invoke(task);
                    Logger.Info($"[下载任务] 任务完成: {task.VersionId}");
                }
                else if (e.PropertyName == nameof(IDownloadTask.IsFailed) && task.IsFailed)
                {
                    TaskFailed?.Invoke(task);
                    Logger.Error($"[下载任务] 任务失败: {task.VersionId}, 错误: {task.ErrorMessage}");
                }
            }
        }
    }
}