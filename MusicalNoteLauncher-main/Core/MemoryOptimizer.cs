using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// PCL 风格内存优化器，支持管理员提权子进程以进行系统级优化。
    /// </summary>
    public static class MemoryOptimizer
    {
        // —— Windows API: 进程工作集 ——
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

        // —— Windows API: 系统文件缓存清除 ——
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int SetSystemFileCacheSize(IntPtr minimumWorkingSet, IntPtr maximumWorkingSet, int flags);

        private const int SystemCacheInformation = 0x15;
        private const int MemoryPurgeStandbyList = 4;

        // —— 权限常量 ——
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint PROCESS_ALL_ACCESS_ADMIN = PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
                                                        PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_SET_QUOTA;

        // Mem Reduct 路径
        private static readonly string[] _memReductPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mem Reduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mem Reduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MemReduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MemReduct", "memreduct.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Mem Reduct", "memreduct.exe"),
        };

        public static event Action<string> Log;

        /// <summary>优化结果统计</summary>
        public class OptimizationResult
        {
            public long FreedMb { get; set; }
            public long MemBeforeMb { get; set; }
            public long MemAfterMb { get; set; }
            public int ProcessesTrimmed { get; set; }
            public int ProcessesSkipped { get; set; }
            public bool IsElevated { get; set; }
            public bool MemReductUsed { get; set; }
            public bool SystemCacheCleared { get; set; }
        }

        // —— 公开入口 ——

        /// <summary>
        /// 检查当前是否为管理员权限
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 主入口：自动检测管理员权限，必要时提权。返回优化结果。
        /// 用于 UI 按钮点击等交互场景。
        /// </summary>
        public static async Task<OptimizationResult> OptimizeAsync(IProgress<(double percent, string status)> progress = null)
        {
            if (!IsAdministrator())
            {
                // —— 非管理员 → 提权到 admin 子进程 ——
                progress?.Report((0.0, "正在请求管理员权限..."));
                long memBefore = GetAvailablePhysicalMemoryMb();
                var elevatedResult = await ElevateAndOptimizeAsync();
                long memAfter = GetAvailablePhysicalMemoryMb();

                // 优先使用子进程退出码报告的释放量（子进程内部测量更准确）
                long freed = elevatedResult.FreedMb > 0
                    ? elevatedResult.FreedMb
                    : (memAfter > memBefore ? memAfter - memBefore : 0);

                return new OptimizationResult
                {
                    FreedMb = freed,
                    MemBeforeMb = memBefore,
                    MemAfterMb = memAfter,
                    ProcessesTrimmed = 0,
                    ProcessesSkipped = 0,
                    IsElevated = elevatedResult.IsElevated,
                    MemReductUsed = elevatedResult.MemReductUsed,
                    SystemCacheCleared = elevatedResult.SystemCacheCleared
                };
            }
            else
            {
                // —— 已是管理员 → 直接执行全部优化 ——
                return await OptimizeCoreAsync(progress);
            }
        }

        /// <summary>
        /// 静默优化（用于 --memory-optimize 子进程模式），返回完整结果。
        /// </summary>
        public static async Task<OptimizationResult> OptimizeSilentAsync()
        {
            return await OptimizeCoreAsync(null);
        }

        // —— 内部实现 ——

        /// <summary>
        /// 管理员提权：以 runas 启动自身子进程，子进程执行优化后退出码返回释放内存量。
        /// </summary>
        private static async Task<OptimizationResult> ElevateAndOptimizeAsync()
        {
            var result = new OptimizationResult();
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            try
            {
                // 将 Process 的同步等待移到线程池，避免阻塞 UI 线程
                await Task.Run(() =>
                {
                    using (var p = new Process())
                    {
                        p.StartInfo.FileName = exePath;
                        p.StartInfo.Arguments = "--memory-optimize";
                        p.StartInfo.UseShellExecute = true;
                        p.StartInfo.Verb = "runas"; // 触发 UAC
                        p.StartInfo.CreateNoWindow = true;
                        p.Start();

                        // 等待提权子进程完成（最多60秒）
                        if (!p.WaitForExit(60000))
                        {
                            try { p.Kill(); } catch { }
                            Log?.Invoke("[内存优化] 提权子进程超时");
                            return;
                        }

                        int exitCode = p.ExitCode;
                        if (exitCode > 0)
                        {
                            result.FreedMb = exitCode;
                            result.IsElevated = true;
                        }
                        else if (exitCode == 0)
                        {
                            result.FreedMb = 0;
                            result.IsElevated = true;
                        }

                        Log?.Invoke($"[内存优化] 提权子进程退出码: {exitCode}");
                    }
                });
            }
            catch (Win32Exception) { /* 用户取消了 UAC */ }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] 提权失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 核心优化逻辑，执行全部优化步骤。
        /// </summary>
        private static async Task<OptimizationResult> OptimizeCoreAsync(IProgress<(double percent, string status)> progress)
        {
            var result = new OptimizationResult();
            result.MemBeforeMb = GetAvailablePhysicalMemoryMb();

            // 步骤1: .NET GC
            progress?.Report((0.10, "正在清理 .NET 托管内存..."));
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            // 步骤2: 清除系统文件缓存（管理员）
            progress?.Report((0.25, "正在清除系统文件缓存..."));
            await Task.Run(() =>
            {
                result.SystemCacheCleared = ClearSystemFileCache();
            });

            // 步骤3: 清除备用内存列表（管理员）
            progress?.Report((0.35, "正在清除备用内存..."));
            await Task.Run(() => PurgeStandbyList());

            // 步骤4: 修剪当前进程
            progress?.Report((0.40, "正在修剪当前进程工作集..."));
            await Task.Run(() => TrimCurrentProcessWorkingSet());

            // 步骤5: 并行修剪所有系统进程（管理员）—— 这是耗时最长的步骤
            progress?.Report((0.45, "正在修剪全系统进程工作集..."));
            await Task.Run(() =>
            {
                int trimmed, skipped;
                TrimAllProcessesWorkingSet(progress, out trimmed, out skipped);
                result.ProcessesTrimmed = trimmed;
                result.ProcessesSkipped = skipped;
            });

            // 步骤6: 尝试 Mem Reduct（可选）
            progress?.Report((0.80, "正在调用 Mem Reduct..."));
            await Task.Run(() =>
            {
                result.MemReductUsed = TryRunMemReduct();
            });

            // 步骤7: 最终 GC
            progress?.Report((0.90, "最终清理..."));
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            result.MemAfterMb = GetAvailablePhysicalMemoryMb();
            result.FreedMb = result.MemAfterMb > result.MemBeforeMb ? result.MemAfterMb - result.MemBeforeMb : 0;
            result.IsElevated = IsAdministrator();

            progress?.Report((1.0, "内存优化完成"));
            Log?.Invoke($"[内存优化] 完成！释放: {result.FreedMb} MB ({result.MemBeforeMb} → {result.MemAfterMb}), 修剪: {result.ProcessesTrimmed} 进程");

            return result;
        }

        // —— 内存工具方法 ——

        /// <summary>获取可用物理内存（MB）</summary>
        public static long GetAvailablePhysicalMemoryMb()
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)(memStatus.ullAvailPhys / (1024 * 1024));
            }
            return 0;
        }

        /// <summary>获取已用物理内存百分比</summary>
        public static int GetMemoryLoadPercent()
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (int)memStatus.dwMemoryLoad;
            }
            return 0;
        }

        // —— 优化步骤 ——

        /// <summary>清除系统文件缓存</summary>
        private static bool ClearSystemFileCache()
        {
            try
            {
                // 将系统文件缓存降到最低再恢复，迫使缓存页释放
                IntPtr min = new IntPtr(1);
                IntPtr max = new IntPtr(1);
                SetSystemFileCacheSize(min, max, 0);
                System.Threading.Thread.Sleep(50);
                SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] 清除系统文件缓存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>清除备用内存列表（需要管理员权限）</summary>
        private static void PurgeStandbyList()
        {
            try
            {
                int result = NtSetSystemInformation(SystemCacheInformation,
                    IntPtr.Zero, 0);
                if (result != 0)
                {
                    Log?.Invoke($"[内存优化] PurgeStandbyList 返回: 0x{result:X8}");
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] PurgeStandbyList 失败: {ex.Message}");
            }
        }

        /// <summary>修剪当前进程工作集</summary>
        private static void TrimCurrentProcessWorkingSet()
        {
            try
            {
                IntPtr hProcess = GetCurrentProcess();
                SetProcessWorkingSetSize(hProcess, new IntPtr(-1), new IntPtr(-1));
                EmptyWorkingSet(hProcess);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] 修剪当前进程失败: {ex.Message}");
            }
        }

        /// <summary>并行修剪所有系统进程的工作集</summary>
        private static void TrimAllProcessesWorkingSet(IProgress<(double, string)> progress,
            out int trimmed, out int skipped)
        {
            trimmed = 0;
            skipped = 0;

            try
            {
                uint[] processIds = new uint[4096];
                uint bytesReturned;

                if (!EnumProcesses(processIds, (uint)(processIds.Length * sizeof(uint)), out bytesReturned))
                    return;

                int processCount = (int)(bytesReturned / sizeof(uint));
                bool isAdmin = IsAdministrator();
                uint access = isAdmin ? PROCESS_SET_QUOTA : (PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION);

                // 过滤出有效 PID 列表
                var pids = new List<uint>(processCount);
                for (int i = 0; i < processCount; i++)
                {
                    uint pid = processIds[i];
                    if (pid > 4) pids.Add(pid); // 跳过 0(Idle) 和 4(System)
                }

                int totalTrimmed = 0;
                int totalSkipped = 0;
                var lockObj = new object();

                Parallel.ForEach(pids, pid =>
                {
                    try
                    {
                        IntPtr hProcess = OpenProcess(access, false, pid);
                        if (hProcess != IntPtr.Zero)
                        {
                            if (isAdmin)
                                SetProcessWorkingSetSize(hProcess, new IntPtr(-1), new IntPtr(-1));
                            EmptyWorkingSet(hProcess);
                            CloseHandle(hProcess);
                            Interlocked.Increment(ref totalTrimmed);
                        }
                        else
                        {
                            Interlocked.Increment(ref totalSkipped);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref totalSkipped);
                    }
                });

                trimmed = totalTrimmed;
                skipped = totalSkipped;

                Log?.Invoke($"[内存优化] 进程修剪完成: {trimmed} 成功, {skipped} 跳过 (共 {pids.Count} 进程)");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] 修剪系统进程异常: {ex.Message}");
            }
        }

        // —— Mem Reduct ——

        private static bool TryRunMemReduct()
        {
            string exePath = FindMemReductPath();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return false;

            try
            {
                using (var p = new Process())
                {
                    p.StartInfo.FileName = exePath;
                    p.StartInfo.Arguments = "/clean /silent";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.Start();
                    p.WaitForExit(3000);

                    if (!p.HasExited)
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[内存优化] Mem Reduct 失败: {ex.Message}");
                return false;
            }
        }

        private static string FindMemReductPath()
        {
            foreach (string path in _memReductPaths)
            {
                if (File.Exists(path)) return path;
            }

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
                                string displayName = subKey?.GetValue("DisplayName") as string;
                                if (!string.IsNullOrEmpty(displayName) &&
                                    displayName.IndexOf("Mem Reduct", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    string location = subKey.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrEmpty(location))
                                    {
                                        string ep = Path.Combine(location, "memreduct.exe");
                                        if (File.Exists(ep)) return ep;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        // —— 结构体 ——

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
