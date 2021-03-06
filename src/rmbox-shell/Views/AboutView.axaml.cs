using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using ReactiveUI;
using Ruminoid.Toolbox.Utils.Extensions;

namespace Ruminoid.Toolbox.Shell.Views
{
    public class AboutView : UserControl
    {
        public AboutView()
        {
            DataContext = new AboutViewModel();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class AboutViewModel : ReactiveObject
    {
        [UsedImplicitly]
        public string VersionSummary { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        [UsedImplicitly]
        public string VersionDetail { get; } = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        [UsedImplicitly]
        public string ReleaseNoteMarkdown { get; } =
            typeof(AboutView).Assembly
                .GetManifestResourceStream(
                    "Ruminoid.Toolbox.Shell.Resources.Markdowns.ReleaseNote.md")
                .ReadStreamToEnd();
    }
}
