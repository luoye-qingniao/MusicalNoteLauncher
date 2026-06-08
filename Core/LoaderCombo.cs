using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class LoaderCombo : LoaderBase
    {
        private readonly List<LoaderBase> _loaders = new List<LoaderBase>();

        public IReadOnlyList<LoaderBase> Loaders => _loaders.AsReadOnly();

        public LoaderCombo(string name, params LoaderBase[] loaders)
        {
            Name = name;
            if (loaders != null)
            {
                _loaders.AddRange(loaders);
            }
        }

        public void AddLoader(LoaderBase loader)
        {
            if (loader != null)
            {
                _loaders.Add(loader);
            }
        }

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            OnStateChanged(LoaderState.Running);

            try
            {
                int totalLoaders = _loaders.Count;
                int completedLoaders = 0;

                foreach (var loader in _loaders)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        OnStateChanged(LoaderState.Cancelled);
                        return;
                    }

                    loader.ProgressChanged += (progress) =>
                    {
                        double totalProgress = ((completedLoaders + progress / 100.0) / totalLoaders) * 100;
                        OnProgressChanged(totalProgress);
                    };

                    await loader.RunAsync(cancellationToken);

                    if (loader.State == LoaderState.Failed)
                    {
                        OnFailed(loader.Error);
                        return;
                    }

                    completedLoaders++;
                    OnProgressChanged((completedLoaders / (double)totalLoaders) * 100);
                }

                OnCompleted();
            }
            catch (OperationCanceledException)
            {
                OnStateChanged(LoaderState.Cancelled);
            }
            catch (Exception ex)
            {
                OnFailed(ex);
            }
        }
    }
}
