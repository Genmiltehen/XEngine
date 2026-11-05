using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Systems;

namespace XEngine.Common.Transform;

public sealed class TransformSystem : ISystem
{
    private readonly struct UpdateState(World world, ComponentHandle<Transform2D> transform)
    {
        public World World { get; } = world;
        public ComponentHandle<Transform2D> Transform { get; } = transform;
    }

    private static void UpdateRoot(in UpdateState state, EntityId entity, ref Node node)
    {
        if (node.Parent.IsNone) UpdateSubtree(state.World, state.Transform, entity, Matrix3x2.Identity);
    }

    public void Update(World world, float dt)
    {
        _ = dt;
        var state = new UpdateState(world, world.Handle<Transform2D>());
        world.Query<Node>().ForEach(in state, UpdateRoot);
    }

    private static void UpdateSubtree(World world, ComponentHandle<Transform2D> transform, EntityId entity, Matrix3x2 parentWorld)
    {
        Matrix3x2 childParentWorld = parentWorld;
        if (world.Has(entity, transform))
        {
            ref Transform2D value = ref world.Get(entity, transform);
            value.World = value.LocalMatrix * parentWorld;
            childParentWorld = value.World;
        }
        foreach (EntityId child in world.Children(entity)) UpdateSubtree(world, transform, child, childParentWorld);
    }
}
