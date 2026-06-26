using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// PCL 风格内存优化器，通过 Windows API 修剪进程工作集 + 调用 Mem Reduct（如果已安装）
    /// </summary>
    public static class MemoryOptimizer
    {
        // Windows API: 清空进程工作集（将内存页换出到页面文件）
        [DllImport("kernel32.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("psapi.dll")]
        private static extern bool EnumProcesses([Out] uint[] processIds, uint arraySizeBytes, out uint bytesReturned);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualLock(IntPtr lpAddress, IntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualUnlock(IntPtr lpAddress, IntPtr dwSize);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint PROCESS_ACCESS = PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
                                             PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_SET_QUOTA;

        // Mem Reduct 常见安装路径
        private static readonly string[] _memReductPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mem Reduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mem Reduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MemReduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MemReduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Mem Reduct", "memreduct.exe"),
        };

        public static event Action<string> Log;

        /// <summary>
        /// PCL 风格内存优化主入口。返回优化释放的内存（MB）。
        /// </summary>
        public static async Task<long> OptimizeAsync(IProgress<(double percent, string status)> progress = null)
        {
            long memBefore = GetAvailablePhysicalMemoryMb();
            progress?.Report((0.05, "正在清理 .NET 托管内存..."));

            // 步骤1：清理 .NET 托管内存
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
            Log?.Invoke("[内存优化] .NET GC 完成");

            progress?.Report((0.15, "正在修剪当前进程工作集..."));
            await Task.Delay(100);

            // 步骤2：修剪当前进程工作集
            await Task.Run(() => TrimCurrentProcessWorkingSet());
            Log?.Invoke("[内存优化] 当前进程工作集已修剪");

            progress?.Report((0.25, "正在修剪系统进程工作集..."));

            // 步骤3：修剪所有系统进程工作集（需要管理员权限）
            await Task.Run(() => TrimAllProcessesWorkingSet(progress));
            Log?.Invoke("[内存优化] 系统进程工作集修剪完成");

            progress?.Report((0.50, "正在调用 Mem Reduct..."));

            // 步骤4：尝试调用 Mem Reduct 进行深度内存清理
            bool memReductRan = false;
            await Task.Run(() =>
            {
                memReductRan = TryRunMemReduct();
            });

            if (memReductRan)
                Log?.Invoke("[内存优化] Mem Reduct 已执行");
            else
                Log?.Invoke("[内存优化] 未找到 Mem Reduct，使用内置方法完成");

            progress?.Report((0.85, "正在进行最终清理..."));

            // 步骤5：最后再次清理 .NET 内存
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
            await Task.Delay(200);

            long memAfter = GetAvailablePhysicalMemoryMb();
            long freed = memAfter > memBefore ? memAfter - memBefore : 0;

            progress?.Report((1.0, "内存优化完成"));
            Log?.Invoke($"[内存优化] 完成！释放内存: {freed} MB (优化前: {memBefore} MB → 优化后: {memAfter} MB)");

            return freed;
        }

        /// <summary>
        /// 获取可用物理内存（MB）
        /// </summary>
        public static long GetAvailablePhysicalMemoryMb()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)(memStatus.ullAvailPhys / (1024 * 1024));
            }
            return 0;
        }

        /// <summary>
        /// 修剪当前进程工作集
        /// </summary>
        private static void TrimCurrentProcessWorkingSet()
        {
            try
            {
                IntPtr hProcess = GetCurrentProcess();
                // 将工作集大小设为 -1,-1 会先清空再恢复，效果比单纯 EmptyWorkingSet 更强
                SetProcessWorkingSetSize(hProcess, new IntPtr(-1), new IntPtr(-1));
                EmptyWorkingSet(hProcess);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] 修剪当前进程失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 修剪所有系统进程的工作集。需要管理员权限才能获得最佳效果。
        /// </summary>
        private static void TrimAllProcessesWorkingSet(IProgress<(double, string)> progress = null)
        {
            try
            {
                uint[] processIds = new uint[4096];
                uint bytesReturned;

                if (!EnumProcesses(processIds, (uint)(processIds.Length * sizeof(uint)), out bytesReturned))
                {
                    Log?.Invoke("[内存优化] 枚举进程失败（可能需要管理员权限）");
                    return;
                }

                int processCount = (int)(bytesReturned / sizeof(uint));
                int trimmed = 0;
                int failed = 0;

                for (int i = 0; i < processCount; i++)
                {
                    uint pid = processIds[i];
                    if (pid == 0) continue;

                    try
                    {
                        IntPtr hProcess = OpenProcess(PROCESS_ACCESS, false, pid);
                        if (hProcess != IntPtr.Zero)
                        {
                            EmptyWorkingSet(hProcess);
                            CloseHandle(hProcess);
                            trimmed++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch
                    {
                        failed++;
                    }

                    // 更新进度 (25% → 50%)
                    if (i % 100 == 0 && progress != null)
                    {
                        double pct = 0.25 + 0.25 * (i / (double)processCount);
                        progress.Report((pct, $"修剪进程工作集 ({i}/{processCount})..."));
                    }
                }

                Log?.Invoke($"[内存优化] 已修剪 {trimmed} 个进程，{failed} 个跳过");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] 修剪系统进程失败: {ex.Message}（需要管理员权限）");
            }
        }

        /// <summary>
        /// 尝试查找并运行 Mem Reduct
        /// </summary>
        private static bool TryRunMemReduct()
        {
            // 先尝试从注册表查找
            string exePath = FindMemReductPath();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = exePath;
                    p.StartInfo.Arguments = "/clean /silent";  // Mem Reduct 静默清理命令行参数
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.Start();
                    p.WaitForExit(10000);  // 10秒超时

                    if (!p.HasExited)
                    {
                        try { p.Kill(); } catch { }
                        Log?.Invoke("[内存优化] Mem Reduct 超时，已终止");
                        return false;
                    }

                    Log?.Invoke($"[内存优化] Mem Reduct 退出码: {p.ExitCode}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] Mem Reduct 调用失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 查找 Mem Reduct 可执行文件路径
        /// </summary>
        private static string FindMemReductPath()
        {
            // 1. 检查常见安装路径
            foreach (string path in _memReductPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // 2. 尝试从注册表查找（HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall）
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (string subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey != null)
                                {
                                    string displayName = subKey.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(displayName) &&
                                        displayName.IndexOf("Mem Reduct", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        string installLocation = subKey.GetValue("InstallLocation") as string;
                                        if (!string.IsNullOrEmpty(installLocation))
                                        {
                                            string exePath = Path.Combine(installLocation, "memreduct.exe");
                                            if (File.Exists(exePath))
                                                return exePath;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. 尝试从 PATH 查找
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "memreduct.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        string result = p.StandardOutput.ReadLine();
                        p.WaitForExit();
                        if (!string.IsNullOrEmpty(result) && File.Exists(result))
                            return result;
                    }
                }
            }
            catch { }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
