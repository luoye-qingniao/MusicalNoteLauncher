using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicalNoteLauncher.Pages
{
    public class InputBox : Window
    {
        public string ResponseText { get; private set; }

        public InputBox(string title, string message, string defaultValue = "")
        {
            base.Title = title;
            base.Width = 350.0;
            base.Height = 150.0;
            base.WindowStyle = WindowStyle.ToolWindow;
            base.ResizeMode = ResizeMode.NoResize;
            base.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Grid grid = new Grid();
            grid.Margin = new Thickness(10.0);
            base.Content = grid;

            RowDefinition value = new RowDefinition { Height = GridLength.Auto };
            RowDefinition value2 = new RowDefinition { Height = GridLength.Auto };
            RowDefinition value3 = new RowDefinition { Height = GridLength.Auto };
            grid.RowDefinitions.Add(value);
            grid.RowDefinitions.Add(value2);
            grid.RowDefinitions.Add(value3);

            Label element = new Label
            {
                Content = message,
                Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
            };
            Grid.SetRow(element, 0);
            grid.Children.Add(element);

            TextBox txtInput = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
            };
            Grid.SetRow(txtInput, 1);
            grid.Children.Add(txtInput);

            Button button = new Button
            {
                Content = "确定",
                Width = 75.0,
                Margin = new Thickness(0.0, 0.0, 5.0, 0.0)
            };
            button.Click += delegate (object s, RoutedEventArgs e)
            {
                this.ResponseText = txtInput.Text;
                this.DialogResult = new bool?(true);
            };

            Button button2 = new Button
            {
                Content = "取消",
                Width = 75.0
            };
            button2.Click += delegate (object s, RoutedEventArgs e)
            {
                base.DialogResult = new bool?(false);
            };

            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            stackPanel.Children.Add(button);
            stackPanel.Children.Add(button2);
            Grid.SetRow(stackPanel, 2);
            grid.Children.Add(stackPanel);

            base.Loaded += delegate (object s, RoutedEventArgs e)
            {
                txtInput.Focus();
            };
        }
    }
}


