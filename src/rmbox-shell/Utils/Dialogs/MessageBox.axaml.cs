using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Ruminoid.Toolbox.Shell.Utils.Dialogs
{
    public class MessageBox : Window
    {
        #region Constructor

        public MessageBox()
        {
            throw new InvalidOperationException();
        }

        private MessageBox(
            string title,
            string content)
        {
            InitializeComponent();

            this.FindControl<TextBlock>("TitleBlock").Text = title;
            this.FindControl<TextBlock>("ContentBlock").Text = content;

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion

        public static Task<bool> ShowAndGetResult(
            string title,
            string content,
            Window parent)
        {
            MessageBox window = new(title, content);
            return window.ShowDialog<bool>(parent);
        }

        private void YesClick(object sender, RoutedEventArgs e) => Close(true);

        private void NoClick(object sender, RoutedEventArgs e) => Close(false);
    }
}