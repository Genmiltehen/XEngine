namespace XEngine.Core.Ecs;


// State + Entity + Components
public delegate void StateEntityQueryAction<TState, T1>(in TState state, EntityId entity, ref T1 component1)
    where T1 : struct, IComponent;

public delegate void StateEntityQueryAction<TState, T1, T2>(in TState state, EntityId entity, ref T1 component1, ref T2 component2)
    where T1 : struct, IComponent
    where T2 : struct, IComponent;

public delegate void StateEntityQueryAction<TState, T1, T2, T3>(in TState state, EntityId entity, ref T1 component1, ref T2 component2, ref T3 component3)
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent;
