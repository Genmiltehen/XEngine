namespace XEngine.Core.Ecs;

internal struct EntityRecord
{
    public int Version;
    public bool Alive;
    public Archetype? Archetype;
    public int Row;
}
