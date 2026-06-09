using System;
using System.Windows.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class DownloadPage : UserControl
    {
        public DownloadPage()
        {
            InitializeComponent();
        }

        private void BtnDownloadCategory_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                switch (tag)
                {
                    case "versions":
                        AppContext.NavigateTo("GameVersions");
                        break;
                    case "packs":
                        AppContext.NavigateTo("Modpacks");
                        break;
                    case "mods":
                        AppContext.NavigateTo("ModsPage");
                        break;
                    case "shaders":
                        AppContext.NavigateTo("Shaders");
                        break;
                    case "datapacks":
                        AppContext.NavigateTo("Datapacks");
                        break;
                    case "dependencies":
                        AppContext.NavigateTo("Dependencies");
                        break;
                }
            }
        }

        private void BtnStartDownload_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string url = txtDownloadUrl.Text;
            if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                AppContext.NavigateTo("DownloadTask");
            }
        }
    }
}
