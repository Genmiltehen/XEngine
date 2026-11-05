using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace XEngine.Core.Ecs;

public sealed class ComponentRegistry
{
    private readonly Dictionary<Type, ComponentType> _byType = [];
    private readonly Dictionary<string, ComponentType> _byName = [];
    private readonly ComponentType?[] _byId = new ComponentType?[ComponentMask.Capacity];
    private readonly List<ComponentType> _types = [];
    private readonly Lock _lock = new();
    private FrozenDictionary<Type, ComponentType>? _frozenByType;
    private FrozenDictionary<string, ComponentType>? _frozenByName;

    public int Count => _types.Count;
    public bool IsFrozen { get; private set; }
    public IReadOnlyList<ComponentType> Types => _types;

    public ComponentType GetById(int id)
    {
        ComponentType? type = (uint)id < (uint)_byId.Length ? _byId[id] : null;
        return type ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Component type is not registered.");
    }

    public bool TryGet(string name, out ComponentType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_frozenByName is not null && _frozenByName.TryGetValue(name, out ComponentType? found))
        {
            type = found;
            return true;
        }
        lock (_lock)
        {
            if (_byName.TryGetValue(name, out found))
            {
                type = found;
                return true;
            }
        }

        type = null!;
        return false;
    }

    public bool TryGet<T>(out ComponentType type) where T : struct, IComponent
    {
        if (_frozenByType is not null && _frozenByType.TryGetValue(typeof(T), out ComponentType? found))
        {
            type = found;
            return true;
        }
        lock (_lock)
        {
            if (_byType.TryGetValue(typeof(T), out found))
            {
                type = found;
                return true;
            }
        }
        type = null!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ComponentType Get<T>() where T : struct, IComponent
    {
        return ReferenceEquals(Lookup<T>.Registry, this) && Lookup<T>.Type is not null ? Lookup<T>.Type : GetSlow<T>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ComponentType GetSlow<T>() where T : struct, IComponent
    {
        ComponentType type;
        if (_frozenByType is not null && _frozenByType.TryGetValue(typeof(T), out ComponentType? frozen)) type = frozen;
        else
            lock (_lock)
                type = _byType.TryGetValue(typeof(T), out ComponentType? registered)
                    ? registered
                    : IsFrozen
                    ? throw new InvalidOperationException($"Component {typeof(T).FullName} is not registered. Register it in a module's ConfigureComponents.")
                    : RegisterCore<T>(typeof(T).FullName ?? typeof(T).Name);
        Lookup<T>.Registry = this;
        Lookup<T>.Type = type;
        return type;
    }

    internal ComponentType Register<T>(string name) where T : struct, IComponent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_lock) return IsFrozen
                ? throw new InvalidOperationException("Component registry is frozen. Register components in a module's ConfigureComponents.")
                : RegisterCore<T>(name);
    }

    internal void Freeze()
    {
        lock (_lock) if (IsFrozen) return;
            _frozenByType = _byType.ToFrozenDictionary();
            _frozenByName = _byName.ToFrozenDictionary(StringComparer.Ordinal);
            IsFrozen = true;
    }

    private static class Lookup<T> where T : struct, IComponent
    {
        [ThreadStatic] public static ComponentRegistry? Registry;
        [ThreadStatic] public static ComponentType? Type;
    }

    private ComponentType RegisterCore<T>(string name) where T : struct, IComponent
    {
        Type runtimeType = typeof(T);
        if (_byType.TryGetValue(runtimeType, out ComponentType? existing)) return existing.Name == name ? existing
                : throw new InvalidOperationException($"Component type {runtimeType.FullName} is already registered as '{existing.Name}'.");
        if (_byName.TryGetValue(name, out ComponentType? nameOwner)) throw new InvalidOperationException($"Component name '{name}' is already registered for {nameOwner.RuntimeType.FullName}.");
        if (_types.Count >= ComponentMask.Capacity) throw new InvalidOperationException($"Component type limit exceeded ({ComponentMask.Capacity}).");
        var type = new ComponentType(_types.Count, name, runtimeType, capacity => new Column<T>(capacity));
        _byId[type.Id] = type;
        _byType.Add(runtimeType, type);
        _byName.Add(name, type);
        _types.Add(type);
        return type;
    }
}
