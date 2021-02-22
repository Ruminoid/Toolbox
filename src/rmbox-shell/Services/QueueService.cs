﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;
using Ruminoid.Toolbox.Shell.ViewModels;

namespace Ruminoid.Toolbox.Shell.Services
{
    public class QueueService : ReactiveObject, ISourceCache<ProjectViewModel, Guid>
    {
        #region Constructor

        public QueueService()
        {
            _runner = new RunnerService(this);

            Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(TriggerProjectPush);
        }

        #endregion

        #region Data

        private readonly SourceCache<ProjectViewModel, Guid> _items = new(x => x.Id);

        #endregion

        #region Services

        private readonly RunnerService _runner;

        #endregion

        #region Current

        private ProjectViewModel _currentProject;

        public ProjectViewModel CurrentProject
        {
            get => _currentProject;
            internal set
            {
                if (value is null)
                {
                    this.RaiseAndSetIfChanged(ref _currentProject, null);
                    return;
                }

                if (value.Status != ProjectStatus.Queued)
                    throw new InvalidOperationException();

                value.Status = ProjectStatus.Running;
                value.IsIndeterminate = true;
                value.Summary = "准备启动运行";

                value
                    .WhenAnyValue(x => x.Status)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(TriggerProjectUpdate);

                this.RaiseAndSetIfChanged(ref _currentProject, value);
            }
        }

        #endregion

        #region Dispatcher

        private void TriggerProjectPush(IChangeSet<ProjectViewModel, Guid> obj)
        {
            if (!_queueRunning || CurrentProject is not null)
                return;

            ProjectViewModel queuedItem =
                Items.FirstOrDefault(x => x.Status == ProjectStatus.Queued);

            if (queuedItem is not null)
                CurrentProject = queuedItem;
        }

        private void TriggerProjectUpdate(ProjectStatus status)
        {
            if (status != ProjectStatus.Completed) return;

            CurrentProject = Items.FirstOrDefault(x => x.Status == ProjectStatus.Queued);
        }
        
        #endregion

        #region Status

        private bool _queueRunning = true;

        #endregion

        #region Queue Operations

        public void Start() => _queueRunning = true;

        public void Stop() => _queueRunning = false;

        public void Kill() => _runner.Kill();

        public void Skip()
        {
            if (CurrentProject.Status != ProjectStatus.Running)
                CurrentProject = null;
        }

        public void Clear() => _items.Clear();

        #endregion

        // QueueService not extends SourceCache because it extends ReactiveObject.

        #region SourceCache

        public IObservable<Change<ProjectViewModel, Guid>> Watch(Guid key) => _items.Watch(key);

        public IObservable<IChangeSet<ProjectViewModel, Guid>> Connect(Func<ProjectViewModel, bool> predicate = null) =>
            _items.Connect(predicate);

        public IObservable<IChangeSet<ProjectViewModel, Guid>> Preview(Func<ProjectViewModel, bool> predicate = null) =>
            _items.Preview(predicate);

        public IObservable<int> CountChanged => _items.CountChanged;

        public void Dispose() => _items.Dispose();

        public Optional<ProjectViewModel> Lookup(Guid key) => _items.Lookup(key);

        public IEnumerable<Guid> Keys => _items.Keys;

        public IEnumerable<ProjectViewModel> Items => _items.Items;

        public IEnumerable<KeyValuePair<Guid, ProjectViewModel>> KeyValues => _items.KeyValues;

        public int Count => _items.Count;

        public void Edit(Action<ISourceUpdater<ProjectViewModel, Guid>> updateAction) => _items.Edit(updateAction);

        public Func<ProjectViewModel, Guid> KeySelector => _items.KeySelector;

        #endregion
    }
}