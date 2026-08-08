using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Backend;

/// <summary>
/// MNL.Backend 控制台入口。
/// 启动 HTTP 桥接服务器，Electron 主进程读取 stdout 中的端口号后加载前端。
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 初始化 MNL 环境
        try { MNLEnvironment.Initialize(); }
        catch (Exception ex) { Console.Error.WriteLine($"环境初始化失败: {ex.Message}"); }

        Logger.Info("[Backend] MNL.Backend 启动中...");

        var server = new BackendServer();
        var port = await server.StartAsync();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => server.Shutdown();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Shutdown();
        };

        await Task.Delay(-1);
    }
}
