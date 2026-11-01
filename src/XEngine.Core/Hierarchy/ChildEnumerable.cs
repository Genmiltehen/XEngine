using XEngine.Core.Ecs;

namespace XEngine.Core.Hierarchy;

public readonly struct ChildEnumerable(World world, EntityId firstChild)
{
    public Enumerator GetEnumerator() => new(world, firstChild);

    public struct Enumerator(World world, EntityId firstChild)
    {
        private EntityId _current;
        private EntityId _next = firstChild;

        public readonly EntityId Current => _current;

        public bool MoveNext()
        {
            _current = _next;
            if (_current.IsNone) return false;
            _next = world.Get<Node>(_current).NextSibling;
            return true;
        }
    }
}
