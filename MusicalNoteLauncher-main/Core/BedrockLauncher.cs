using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class BedrockLauncher
    {
        public event Action<string> LaunchStatusChanged;
        public event Action<string> LaunchLogReceived;
        public event Action<bool> LaunchCompleted;

        private readonly string _minecraftPath;

        public BedrockLauncher(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
        }

        public async Task<bool> LaunchGameAsync(string versionId)
        {
            try
            {
                LaunchStatusChanged?.Invoke("正在检查基岩版安装...");
                
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                string exePath = Path.Combine(bedrockDir, "Minecraft.Windows.exe");

                if (!File.Exists(exePath))
                {
                    LaunchLogReceived?.Invoke($"错误: 基岩版 {versionId} 未安装");
                    LaunchStatusChanged?.Invoke("启动失败: 版本未安装");
                    LaunchCompleted?.Invoke(false);
                    return false;
                }

                LaunchLogReceived?.Invoke($"基岩版路径: {exePath}");
                LaunchStatusChanged?.Invoke("正在启动基岩版...");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = bedrockDir,
                    UseShellExecute = true
                };

                Process gameProcess = Process.Start(startInfo);
                
                if (gameProcess != null)
                {
                    LaunchLogReceived?.Invoke("基岩版启动成功");
                    LaunchStatusChanged?.Invoke("启动成功");
                    LaunchCompleted?.Invoke(true);
                    
                    await Task.Run(() => gameProcess.WaitForExit());
                    LaunchLogReceived?.Invoke("基岩版已退出");
                    return true;
                }
                else
                {
                    LaunchLogReceived?.Invoke("错误: 无法启动进程");
                    LaunchStatusChanged?.Invoke("启动失败");
                    LaunchCompleted?.Invoke(false);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LaunchLogReceived?.Invoke($"启动失败: {ex.Message}");
                LaunchStatusChanged?.Invoke($"启动失败: {ex.Message}");
                LaunchCompleted?.Invoke(false);
                return false;
            }
        }

        public bool IsVersionInstalled(string versionId)
        {
            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                string exePath = Path.Combine(bedrockDir, "Minecraft.Windows.exe");
                return File.Exists(exePath);
            }
            catch
            {
                return false;
            }
        }
    }
}
