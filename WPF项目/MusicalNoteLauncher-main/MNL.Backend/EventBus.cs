using System.Collections.Concurrent;

namespace MusicalNoteLauncher.Backend;

/// <summary>
/// 线程安全的事件总线，用于 C# → JS SSE 推送。
/// </summary>
public static class EventBus
{
    private static readonly ConcurrentQueue<BridgeEvent> _queue = new();
    private static readonly SemaphoreSlim _signal = new(0, int.MaxValue);

    /// <summary>推送事件到前端</summary>
    public static void Push(string type, object? payload = null)
    {
        _queue.Enqueue(new BridgeEvent { Type = type, Payload = payload });
        try { _signal.Release(); } catch (SemaphoreFullException) { }
    }

    /// <summary>等待并取出一个事件（用于 SSE 流，带超时）</summary>
    public static async Task<BridgeEvent?> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            await _signal.WaitAsync(timeout, ct);
        }
        catch (OperationCanceledException) { return null; }
        // 超时时返回 null（调用方发心跳）
        if (!_signal.Wait(0))
            return null;
        if (_queue.TryDequeue(out var evt))
            return evt;
        return null;
    }

    /// <summary>等待并取出一个事件（用于 SSE 流）</summary>
    public static async Task<BridgeEvent?> WaitAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        if (_queue.TryDequeue(out var evt))
            return evt;
        return null;
    }

    /// <summary>非阻塞尝试取出</summary>
    public static bool TryDequeue(out BridgeEvent evt) => _queue.TryDequeue(out evt);
}

public class BridgeEvent
{
    public string Type { get; set; } = "";
    public object? Payload { get; set; }
}
