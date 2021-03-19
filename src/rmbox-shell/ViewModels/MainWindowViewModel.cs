﻿using System;
using System.Reflection;
using ReactiveUI;
using Ruminoid.Toolbox.Shell.Models;
using Ruminoid.Toolbox.Shell.Services;
using Ruminoid.Toolbox.Shell.Views;
using Splat;

namespace Ruminoid.Toolbox.Shell.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        public MainWindowViewModel(
            MainWindow window)
        {
            _window = window;

            Locator.Current
                .GetService<QueueService>()
                .Connect()
                .Subscribe(_ => CurrentTabIndex = 1);
        }

        private readonly MainWindow _window;

        #region Version

        public string VersionSummary { get; } = $"版本 {Assembly.GetExecutingAssembly().GetName().Version}";

        #endregion

        #region Tab

        private int _currentTabIndex;

        public int CurrentTabIndex
        {
            get => _currentTabIndex;
            set => this.RaiseAndSetIfChanged(ref _currentTabIndex, value);
        }

        #endregion

        #region Commands

        public async void DoCreateNewOperation()
        {
            OperationModel result = await ChooseOperationWindow.ChooseOperation(_window);
            if (result is null) return;
            new OperationWindow(result).Show(_window);
        }

        public void DoCreateNewChain()
        {
            throw new NotImplementedException();
        }

        public void DoCreateService()
        {
            throw new NotImplementedException();
        }

        public void DoShowAboutWindow()
        {
            AboutWindow.ShowAbout(_window);
        }

        public void DoClose() => _window.Close();

        #endregion
    }
}
