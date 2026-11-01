namespace XEngine.Core.Ecs;

public delegate void EntityQueryAction<T1>(EntityId entity, ref T1 component1)
    where T1 : struct, IComponent;

public delegate void EntityQueryAction<T1, T2>(EntityId entity, ref T1 component1, ref T2 component2)
    where T1 : struct, IComponent
    where T2 : struct, IComponent;

public delegate void EntityQueryAction<T1, T2, T3>(EntityId entity, ref T1 component1, ref T2 component2, ref T3 component3)
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent;