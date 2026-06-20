using System;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public abstract class LoaderBase
    {
        public string Name { get; protected set; }
        public LoaderState State { get; protected set; } = LoaderState.Waiting;
        public double Progress { get; protected set; } = 0;
        public string ProgressText { get; protected set; } = "";
        public Exception Error { get; protected set; }
        public CancellationToken CancellationToken { get; protected set; }

        public event Action<double> ProgressChanged;
        public event Action<string> StateChanged;
        public event Action Completed;
        public event Action<Exception> Failed;

        protected void OnProgressChanged(double progress)
        {
            Progress = progress;
            ProgressChanged?.Invoke(progress);
        }

        protected void OnStateChanged(LoaderState state)
        {
            State = state;
            StateChanged?.Invoke(state.ToString());
        }

        protected void OnCompleted()
        {
            State = LoaderState.Completed;
            Completed?.Invoke();
        }

        protected void OnFailed(Exception ex)
        {
            Error = ex;
            State = LoaderState.Failed;
            Failed?.Invoke(ex);
        }

        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }

    public enum LoaderState
    {
        Waiting,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
