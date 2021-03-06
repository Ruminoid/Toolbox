﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;
using Ruminoid.Toolbox.Shell.ViewModels.Project;

namespace Ruminoid.Toolbox.Shell.Services
{
    public class QueueService : ReactiveObject, ISourceCache<ProjectViewModel, Guid>
    {
        #region Constructor

        public QueueService()
        {
            _runner = new RunnerService(this);

            Connect()
                .Subscribe(TriggerProjectPush);
        }

        #endregion

        #region Data

        private readonly SourceCache<ProjectViewModel, Guid> _items = new(x => x.Id);

        #endregion

        #region Dynamic Link
        
        public readonly Subject<string> RunnerOutput = new();
        
        #endregion

        #region Services

        private readonly RunnerService _runner;

        #endregion

        #region Current

        private ProjectViewModel _currentProject;

        public ProjectViewModel CurrentProject
        {
            get => _currentProject;
            private set
            {
                if (value is null)
                {
                    this.RaiseAndSetIfChanged(ref _currentProject, null);
                    return;
                }

                if (value.Status != ProjectStatus.Queued)
                    throw new InvalidOperationException();

                value
                    .WhenAnyValue(x => x.Status)
                    .Subscribe(TriggerProjectUpdate);

                _currentProject = value;
                this.RaisePropertyChanged(nameof(CurrentProject));
            }
        }

        private ProjectStatus _currentProjectStatus = ProjectStatus.Queued;

        public ProjectStatus CurrentProjectStatus
        {
            get => _currentProjectStatus;
            set => this.RaiseAndSetIfChanged(ref _currentProjectStatus, value);
        }

        #endregion

        #region Dispatcher

        private void TriggerProjectPush(IChangeSet<ProjectViewModel, Guid> obj) => PushProject();

        private void PushProject(bool ignore = false)
        {
            if (!ignore && !QueueRunning)
                return;
            if (!ignore && CurrentProject is not null)
                return;

            CurrentProject = Items.FirstOrDefault(x => x.Status == ProjectStatus.Queued);
        }

        private void TriggerProjectUpdate(ProjectStatus status)
        {
            CurrentProjectStatus = status;

            if (status != ProjectStatus.Completed) return;

            PushProject(true);
        }
        
        #endregion

        #region Status

        private bool _queueRunning = true;

        public bool QueueRunning
        {
            get => _queueRunning;
            set => this.RaiseAndSetIfChanged(ref _queueRunning, value);
        }

        #endregion

        #region Queue Operations

        public void Start()
        {
            QueueRunning = true;
            PushProject();
        }

        public void Stop() => QueueRunning = false;

        public void Kill() => _runner.Kill();

        public void Skip()
        {
            if (CurrentProject is null ||
                CurrentProject.Status != ProjectStatus.Running)
                PushProject(true);
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
