using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace WuxiaProj.Framework;

/// <summary>
/// 游戏对象管理器 — 管理"活的实体"的创建、查找与回收。
/// 每个实体拥有唯一 ObjectId，按 ObjectType 分类索引。
/// </summary>
public partial class ObjectManager : Node
{
    public static ObjectManager Instance { get; private set; } = null!;

    /// <summary>id → 实体节点</summary>
    private readonly Dictionary<ObjectId, Node> _objects = new();

    /// <summary>id → 实体类型</summary>
    private readonly Dictionary<ObjectId, ObjectType> _objectTypes = new();

    /// <summary>类型 → 实体列表（快速批量遍历某一类实体）</summary>
    private readonly Dictionary<ObjectType, List<Node>> _byType = new();

    [Signal]
    public delegate void ObjectRegisteredEventHandler(ulong objectIdValue, int objectType);

    [Signal]
    public delegate void ObjectUnregisteredEventHandler(ulong objectIdValue, int objectType);

    public override void _Ready()
    {
        Instance = this;

        foreach (ObjectType type in Enum.GetValues<ObjectType>())
            _byType[type] = new List<Node>();

        GD.Print("[ObjectManager] 已就绪");
    }

    /// <summary>
    /// 注册一个实体到全局索引。返回分配的 ObjectId。
    /// </summary>
    public ObjectId Register(Node node, ObjectType type)
    {
        var id = ObjectId.New();
        _objects[id] = node;
        _objectTypes[id] = type;
        _byType[type].Add(node);
        EmitSignal(SignalName.ObjectRegistered, id.Value, (int)type);
        return id;
    }

    /// <summary>
    /// 从全局索引注销一个实体。
    /// </summary>
    public void Unregister(ObjectId id)
    {
        if (!_objects.Remove(id, out var node))
            return;

        if (_objectTypes.Remove(id, out var type) && _byType.TryGetValue(type, out var list))
        {
            list.Remove(node);
        }

        EmitSignal(SignalName.ObjectUnregistered, id.Value, (int)type);
    }

    /// <summary>
    /// 按 id 查找实体。
    /// </summary>
    public T? Find<T>(ObjectId id) where T : Node
    {
        return _objects.TryGetValue(id, out var node) ? node as T : null;
    }

    /// <summary>
    /// 按类型查找所有实体。
    /// </summary>
    public IReadOnlyList<T> FindByType<T>(ObjectType type) where T : Node
    {
        return _byType.TryGetValue(type, out var list)
            ? list.OfType<T>().ToList()
            : Array.Empty<T>();
    }

    /// <summary>
    /// 从预制场景创建实体（内部调 ResourceManager 异步加载预制）。
    /// </summary>
    public async Task<T> CreateAsync<T>(string prefabLogicName, ObjectType type) where T : Node
    {
        var prefab = await ResourceManager.Instance.LoadAsync<PackedScene>(prefabLogicName);
        var node = prefab.Instantiate<T>();
        Register(node, type);
        return node;
    }

    /// <summary>
    /// 销毁实体。当前直接 QueueFree，后续可改为退回对象池。
    /// </summary>
    public void Destroy(ObjectId id)
    {
        if (!_objects.TryGetValue(id, out var node))
            return;

        Unregister(id);
        node.QueueFree();
    }

    /// <summary>
    /// 当前注册的实体总数。
    /// </summary>
    public int Count => _objects.Count;
}
