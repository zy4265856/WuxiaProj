using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WuxiaProj.Framework.Serialization;

/// <summary>
/// Newtonsoft.Json (Json.NET) 序列化实现。
/// 数据以 JSON 文本格式存储，可读性好，适合策划配置表和调试场景。
/// </summary>
public class JsonImpl : ISerializer
{
    private static readonly UTF8Encoding Utf8NoBom = new(
        encoderShouldEmitUTF8Identifier: false);

    // ═══════════════════════════════════════════
    //  同步 — 序列化
    // ═══════════════════════════════════════════

    public byte[] Serialize<T>(T value)
    {
        var json = JsonConvert.SerializeObject(value);
        return Utf8NoBom.GetBytes(json);
    }

    public void Serialize<T>(Stream stream, T value)
    {
        // 用 StreamWriter 写 UTF-8 无 BOM 文本，leaveOpen 避免关闭外部流
        using var writer = new StreamWriter(stream, Utf8NoBom, leaveOpen: true);
        var json = JsonConvert.SerializeObject(value);
        writer.Write(json);
        writer.Flush();
    }

    public void Serialize<T>(IBufferWriter<byte> writer, T value)
    {
        var json = JsonConvert.SerializeObject(value);
        var byteCount = Utf8NoBom.GetByteCount(json);
        var span = writer.GetSpan(byteCount);
        Utf8NoBom.GetBytes(json, span);
        writer.Advance(byteCount);
    }

    // ═══════════════════════════════════════════
    //  同步 — 反序列化
    // ═══════════════════════════════════════════

    public T? Deserialize<T>(byte[] data)
        => JsonConvert.DeserializeObject<T>(Utf8NoBom.GetString(data));

    public T? Deserialize<T>(ArraySegment<byte> data)
        => JsonConvert.DeserializeObject<T>(
            Utf8NoBom.GetString(data.Array!, data.Offset, data.Count));

    public T? Deserialize<T>(ReadOnlySpan<byte> data)
        => JsonConvert.DeserializeObject<T>(Utf8NoBom.GetString(data));

    public T? Deserialize<T>(Stream stream)
    {
        using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        var json = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<T>(json);
    }

    public T? Deserialize<T>(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
            return JsonConvert.DeserializeObject<T>(
                Utf8NoBom.GetString(sequence.First.Span));

        // 跨段：拷贝到连续数组后解码
        var bytes = sequence.ToArray();
        return JsonConvert.DeserializeObject<T>(Utf8NoBom.GetString(bytes));
    }

    // ═══════════════════════════════════════════
    //  异步 — Stream
    // ═══════════════════════════════════════════

    public async ValueTask SerializeAsync<T>(Stream stream, T value,
        CancellationToken ct = default)
    {
        var json = JsonConvert.SerializeObject(value);
        var bytes = Utf8NoBom.GetBytes(json);
        await stream.WriteAsync(bytes, ct);
    }

    public async ValueTask<T?> DeserializeAsync<T>(Stream stream,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        var json = await reader.ReadToEndAsync(ct);
        return JsonConvert.DeserializeObject<T>(json);
    }

    // ═══════════════════════════════════════════
    //  异步 — Pipeline
    // ═══════════════════════════════════════════

    public async ValueTask SerializeAsync<T>(PipeWriter writer, T value,
        CancellationToken ct = default)
    {
        var json = JsonConvert.SerializeObject(value);
        var bytes = Utf8NoBom.GetBytes(json);
        await writer.WriteAsync(new ReadOnlyMemory<byte>(bytes), ct);
    }

    public async ValueTask<T?> DeserializeAsync<T>(PipeReader reader,
        CancellationToken ct = default)
    {
        var result = await reader.ReadAsync(ct);
        var json = result.Buffer.IsSingleSegment
            ? Utf8NoBom.GetString(result.Buffer.First.Span)
            : Utf8NoBom.GetString(result.Buffer.ToArray());
        reader.AdvanceTo(result.Buffer.End);
        return JsonConvert.DeserializeObject<T>(json);
    }
}
