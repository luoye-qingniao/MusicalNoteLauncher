using System.Windows;

namespace MusicalNoteLauncher
{
    public partial class TestApp : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            var window = new Window
            {
                Title = "Test Window",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.Gray
            };
            
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "Hello World!",
                FontSize = 24,
                Foreground = System.Windows.Media.Brushes.White
            };
            
            var panel = new System.Windows.Controls.StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(textBlock);
            window.Content = panel;
            
            window.Show();
        }
    }
}