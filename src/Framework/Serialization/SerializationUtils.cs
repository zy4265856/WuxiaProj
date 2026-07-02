using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WuxiaProj.Framework.Serialization;

/// <summary>
/// 基于 ISerializer 的扩展工具方法。纯静态扩展，不持有状态。
/// </summary>
public static class SerializationUtils
{
    // ═══════════════════════════════════════════
    //  深拷贝
    // ═══════════════════════════════════════════

    /// <summary>
    /// 通过序列化再反序列化实现深拷贝。
    /// 适用于属性快照、状态回滚点等场景。
    /// </summary>
    public static T? DeepClone<T>(this ISerializer serializer, T value)
    {
        var bytes = serializer.Serialize(value);
        return serializer.Deserialize<T>(bytes);
    }

    // ═══════════════════════════════════════════
    //  便捷文件读写
    // ═══════════════════════════════════════════

    /// <summary>
    /// 序列化并写入文件（覆盖模式）。组合 FileStream + SerializeAsync。
    /// </summary>
    public static async ValueTask SaveToFileAsync<T>(this ISerializer serializer,
        string path, T value, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Create,
            FileAccess.Write, FileShare.None, bufferSize: 4096,
            useAsync: true);
        await serializer.SerializeAsync(stream, value, ct);
    }

    /// <summary>
    /// 从文件读取并反序列化。文件不存在时返回 default(T)。
    /// </summary>
    public static async ValueTask<T?> LoadFromFileAsync<T>(this ISerializer serializer,
        string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            return default;

        await using var stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read, bufferSize: 4096,
            useAsync: true);
        return await serializer.DeserializeAsync<T>(stream, ct);
    }

    // ═══════════════════════════════════════════
    //  Base64 互转（网络传输 / 纯文本存档 / 调试日志）
    // ═══════════════════════════════════════════

    /// <summary>序列化为 Base64 字符串</summary>
    public static string ToBase64<T>(this ISerializer serializer, T value)
    {
        var bytes = serializer.Serialize(value);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>从 Base64 字符串反序列化</summary>
    public static T? FromBase64<T>(this ISerializer serializer, string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return serializer.Deserialize<T>(bytes);
    }

    // ═══════════════════════════════════════════
    //  池化缓冲写入（减少 GC 压力）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 使用 ArrayBufferWriter（内部走 ArrayPool&lt;byte&gt;.Shared）序列化，
    /// 减少高频调用时的 GC 分配压力。
    /// </summary>
    /// <param name="initialCapacity">初始缓冲区大小（默认 64KB）</param>
    public static byte[] SerializePooled<T>(this ISerializer serializer, T value,
        int initialCapacity = 65536)
    {
        var writer = new ArrayBufferWriter<byte>(initialCapacity);
        serializer.Serialize(writer, value);
        return writer.WrittenSpan.ToArray();
    }

    // ═══════════════════════════════════════════
    //  批量序列化
    // ═══════════════════════════════════════════

    /// <summary>批量序列化：将多个对象一次写入同一个字节数组</summary>
    public static byte[] SerializeBatch<T>(this ISerializer serializer,
        IEnumerable<T> values)
    {
        var list = values as List<T> ?? values.ToList();
        return serializer.Serialize(list);
    }

    /// <summary>批量反序列化：从字节数组恢复对象列表</summary>
    public static IReadOnlyList<T?> DeserializeBatch<T>(this ISerializer serializer,
        byte[] data)
    {
        return serializer.Deserialize<List<T?>>(data)
            ?? (IReadOnlyList<T?>)Array.Empty<T?>();
    }

    // ═══════════════════════════════════════════
    //  安全尝试（不抛异常，返回 bool 指示成功）
    // ═══════════════════════════════════════════

    /// <summary>尝试反序列化，失败时返回 false 并输出 default</summary>
    public static bool TryDeserialize<T>(this ISerializer serializer,
        byte[] data, out T? result)
    {
        try
        {
            result = serializer.Deserialize<T>(data);
            return result is not null;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>尝试从文件反序列化，失败时返回 false</summary>
    public static async ValueTask<(bool Success, T? Result)> TryLoadFromFileAsync<T>(
        this ISerializer serializer, string path,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
                return (false, default);

            var result = await serializer.LoadFromFileAsync<T>(path, ct);
            return result is not null
                ? (true, result)
                : (false, default);
        }
        catch
        {
            return (false, default);
        }
    }
}
