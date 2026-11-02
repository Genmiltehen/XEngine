namespace XEngine.Core.Ecs;

public readonly struct Query<T1, T2> where T1 : struct, IComponent where T2 : struct, IComponent
{
    private readonly World _world;
    private readonly ComponentMask _with;
    private readonly ComponentMask _without;

    internal Query(World world, ComponentMask with)
        : this(world, with, ComponentMask.Empty) { }

    private Query(World world, ComponentMask with, ComponentMask without)
    {
        _world = world;
        _with = with;
        _without = without;
    }

    public Query<T1, T2> With<T>()
        where T : struct, IComponent =>
        new(_world, _with.With(_world.Components.Get<T>().Id), _without);

    public Query<T1, T2> Without<T>()
        where T : struct, IComponent =>
        new(_world, _with, _without.With(_world.Components.Get<T>().Id));

    public void ForEach(QueryAction<T1, T2> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        World world = _world;
#if DEBUG
        world.BeginIteration();
        try
        {
#endif
            foreach (Archetype archetype in world.FilterArchetypes(_with, _without))
            {
                Column<T1> first = archetype.GetColumn<T1>();
                Column<T2> second = archetype.GetColumn<T2>();
                for (var row = 0; row < archetype.Count; row++) action(ref first[row], ref second[row]);
            }
#if DEBUG
        }
        finally
        {
            world.EndIteration();
        }
#endif
    }

    public void ForEach(EntityQueryAction<T1, T2> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        World world = _world;
#if DEBUG
        world.BeginIteration();
        try
        {
#endif
            foreach (Archetype archetype in world.FilterArchetypes(_with, _without))
            {
                Column<T1> first = archetype.GetColumn<T1>();
                Column<T2> second = archetype.GetColumn<T2>();
                EntityId[] entities = archetype.Entities;
                for (var row = 0; row < archetype.Count; row++) action(entities[row], ref first[row], ref second[row]);
            }
#if DEBUG
        }
        finally
        {
            world.EndIteration();
        }
#endif
    }

    public void ForEach<TState>(in TState state, StateQueryAction<TState, T1, T2> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        World world = _world;
#if DEBUG
        world.BeginIteration();
        try
        {
#endif
            foreach (Archetype archetype in world.FilterArchetypes(_with, _without))
            {
                Column<T1> first = archetype.GetColumn<T1>();
                Column<T2> second = archetype.GetColumn<T2>();
                for (var row = 0; row < archetype.Count; row++) action(in state, ref first[row], ref second[row]);
            }
#if DEBUG
        }
        finally
        {
            world.EndIteration();
        }
#endif
    }

    public void ForEach<TState>(in TState state, StateEntityQueryAction<TState, T1, T2> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        World world = _world;
#if DEBUG
        world.BeginIteration();
        try
        {
#endif
            foreach (Archetype archetype in world.FilterArchetypes(_with, _without))
            {
                Column<T1> first = archetype.GetColumn<T1>();
                Column<T2> second = archetype.GetColumn<T2>();
                EntityId[] entities = archetype.Entities;
                for (var row = 0; row < archetype.Count; row++) action(in state, entities[row], ref first[row], ref second[row]);
            }
#if DEBUG
        }
        finally
        {
            world.EndIteration();
        }
#endif
    }

    public int Count()
    {
        var count = 0;
        foreach (Archetype archetype in _world.FilterArchetypes(_with, _without)) count += archetype.Count;
        return count;
    }
}
