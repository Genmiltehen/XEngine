using System.Diagnostics.CodeAnalysis;

namespace XEngine.Core.Services;

public sealed class EngineServiceContainer : IDisposable
{
    private readonly Dictionary<Type, object> _services = [];
    private bool _disposed;

    public T Get<T>() where T : class
    {
        ThrowIfDisposed();
        return _services.TryGetValue(typeof(T), out var service)
            ? (T)service
            : throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }

    public bool TryGet<T>([NotNullWhen(true)] out T? service) where T : class
    {
        ThrowIfDisposed();
        if (_services.TryGetValue(typeof(T), out var stored))
        {
            service = (T)stored;
            return true;
        }
        service = null;
        return false;
    }

    public void Set<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        ThrowIfDisposed();
        if (_services.TryGetValue(typeof(T), out var previous))
        {
            _services[typeof(T)] = service;
            if (!ReferenceEquals(previous, service)) DisposeIfNeeded(previous);
            return;
        }
        _services.Add(typeof(T), service);
    }

    public bool Remove<T>() where T : class
    {
        ThrowIfDisposed();
        if (!_services.Remove(typeof(T), out var service)) return false;
        DisposeIfNeeded(service);
        return true;
    }

    private static void DisposeIfNeeded(object? service)
    {
        if (service is IDisposable disposable) disposable.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var service in _services.Values) DisposeIfNeeded(service);
        _services.Clear();
    }
}
