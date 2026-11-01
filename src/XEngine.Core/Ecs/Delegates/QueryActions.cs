namespace XEngine.Core.Ecs;

public delegate void QueryAction<T1>(ref T1 component1)
    where T1 : struct, IComponent;

public delegate void QueryAction<T1, T2>(ref T1 component1, ref T2 component2)
    where T1 : struct, IComponent
    where T2 : struct, IComponent;

public delegate void QueryAction<T1, T2, T3>(ref T1 component1, ref T2 component2, ref T3 component3)
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent;