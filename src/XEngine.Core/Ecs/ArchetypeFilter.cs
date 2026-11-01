namespace XEngine.Core.Ecs;

internal readonly struct ArchetypeFilter(List<Archetype> archetypes, ComponentMask with, ComponentMask without)
{
    public Enumerator GetEnumerator() => new(archetypes, with, without);

    internal struct Enumerator(List<Archetype> archetypes, ComponentMask with, ComponentMask without)
    {
        private int _index = -1;
        public readonly Archetype Current => archetypes[_index];
        public bool MoveNext()
        {
            while (++_index < archetypes.Count)
            {
                Archetype archetype = archetypes[_index];
                if (!archetype.Mask.Matches(with, without)) continue;
                return true;
            }
            return false;
        }
    }
}
