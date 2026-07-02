using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;

namespace WuxiaProj.Framework.Serialization;

/// <summary>
/// MemoryPack 序列化实现。
/// 所有被序列化的类型必须标注 [MemoryPackable] 特性并声明为 partial class。
/// </summary>
public class MemoryPackImpl : ISerializer
{
    // ═══════════════════════════════════════════
    //  同步 — 序列化
    // ═══════════════════════════════════════════

    public byte[] Serialize<T>(T value)
        => MemoryPackSerializer.Serialize(value);

    public void Serialize<T>(Stream stream, T value)
    {
        var bytes = MemoryPackSerializer.Serialize(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    public void Serialize<T>(IBufferWriter<byte> writer, T value)
        => MemoryPackSerializer.Serialize(writer, value);

    // ═══════════════════════════════════════════
    //  同步 — 反序列化
    // ═══════════════════════════════════════════

    public T? Deserialize<T>(byte[] data)
        => MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)data);

    public T? Deserialize<T>(ArraySegment<byte> data)
        => MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)data);

    public T? Deserialize<T>(ReadOnlySpan<byte> data)
        => MemoryPackSerializer.Deserialize<T>(data);

    public T? Deserialize<T>(Stream stream)
    {
        // 小文件/配置场景：一次读空 Stream 再反序列化
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return MemoryPackSerializer.Deserialize<T>(
            new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length));
    }

    public T? Deserialize<T>(ReadOnlySequence<byte> sequence)
        => MemoryPackSerializer.Deserialize<T>(sequence);

    // ═══════════════════════════════════════════
    //  异步 — Stream
    // ═══════════════════════════════════════════

    public async ValueTask SerializeAsync<T>(Stream stream, T value,
        CancellationToken ct = default)
        => await MemoryPackSerializer.SerializeAsync(stream, value,
            cancellationToken: ct);

    public async ValueTask<T?> DeserializeAsync<T>(Stream stream,
        CancellationToken ct = default)
        => await MemoryPackSerializer.DeserializeAsync<T>(stream,
            cancellationToken: ct);

    // ═══════════════════════════════════════════
    //  异步 — Pipeline
    // ═══════════════════════════════════════════

    public async ValueTask SerializeAsync<T>(PipeWriter writer, T value,
        CancellationToken ct = default)
    {
        // PipeWriter 实现了 IBufferWriter<byte>，走零拷贝路径
        MemoryPackSerializer.Serialize<T, PipeWriter>(writer, value);
        await writer.FlushAsync(ct);
    }

    public async ValueTask<T?> DeserializeAsync<T>(PipeReader reader,
        CancellationToken ct = default)
    {
        var result = await reader.ReadAsync(ct);
        var value = MemoryPackSerializer.Deserialize<T>(result.Buffer);
        reader.AdvanceTo(result.Buffer.End);
        return value;
    }
}
