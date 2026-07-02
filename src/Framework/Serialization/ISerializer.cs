using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace WuxiaProj.Framework.Serialization;

/// <summary>
/// 序列化抽象接口。定义"数据 ↔ 字节"的转换契约，不依赖具体序列化库。
/// 通过 ServiceLocator 注册/替换实现（MemoryPack / JSON / FlatBuffer 等）。
/// </summary>
public interface ISerializer
{
    // ═══════════════════════════════════════════
    //  同步 — 序列化
    // ═══════════════════════════════════════════

    /// <summary>序列化为字节数组（小数据快照 / 网络消息）</summary>
    byte[] Serialize<T>(T value);

    /// <summary>序列化到流（内部先序列化再写入流）</summary>
    void Serialize<T>(Stream stream, T value);

    /// <summary>序列化到缓冲写入器（零拷贝，配合 ArrayPool 使用）</summary>
    void Serialize<T>(IBufferWriter<byte> writer, T value);

    // ═══════════════════════════════════════════
    //  同步 — 反序列化
    // ═══════════════════════════════════════════

    /// <summary>从字节数组反序列化</summary>
    T? Deserialize<T>(byte[] data);

    /// <summary>从数组段反序列化（池化缓冲区 / 不从头部开始的数据）</summary>
    T? Deserialize<T>(ArraySegment<byte> data);

    /// <summary>从只读跨度反序列化（零分配，栈上 buffer）</summary>
    T? Deserialize<T>(ReadOnlySpan<byte> data);

    /// <summary>从流反序列化（内部先读取全部字节再反序列化）</summary>
    T? Deserialize<T>(Stream stream);

    /// <summary>从只读字节序列反序列化（Pipeline 场景零拷贝读入）</summary>
    T? Deserialize<T>(ReadOnlySequence<byte> sequence);

    // ═══════════════════════════════════════════
    //  异步 — Stream
    // ═══════════════════════════════════════════

    /// <summary>异步序列化到流</summary>
    ValueTask SerializeAsync<T>(Stream stream, T value,
        CancellationToken ct = default);

    /// <summary>异步从流反序列化</summary>
    ValueTask<T?> DeserializeAsync<T>(Stream stream,
        CancellationToken ct = default);

    // ═══════════════════════════════════════════
    //  异步 — Pipeline（高吞吐）
    // ═══════════════════════════════════════════

    /// <summary>异步序列化到管道写入器（写入后自动 Flush）</summary>
    ValueTask SerializeAsync<T>(PipeWriter writer, T value,
        CancellationToken ct = default);

    /// <summary>异步从管道读取器反序列化（单帧读取）</summary>
    ValueTask<T?> DeserializeAsync<T>(PipeReader reader,
        CancellationToken ct = default);
}
