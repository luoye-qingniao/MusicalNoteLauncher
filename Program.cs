using System;
using System.Threading.Tasks;

namespace MusicalNoteLauncher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Minecraft Launcher ===");
            
            var downloadManager = new Core.DownloadManager(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.minecraft");
            
            try
            {
                Console.WriteLine("正在获取版本列表...");
                
                var versions = await downloadManager.GetRemoteVersionsAsync();
                
                if (versions != null && versions.Count > 0)
                {
                    Console.WriteLine($"成功获取到 {versions.Count} 个版本");
                    Console.WriteLine("离线模式已关闭");
                    
                    foreach (var version in versions)
                    {
                        Console.WriteLine($"  - {version.Id} ({version.DisplayType})");
                    }
                }
                else
                {
                    Console.WriteLine("所有数据源均未返回有效版本数据");
                    Console.WriteLine("离线模式已开启");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动器运行异常: {ex.Message}");
            }
            
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}