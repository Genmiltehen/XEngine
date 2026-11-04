using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Systems;

namespace XEngine.Core.Transforms;

public sealed class TransformSystem : ISystem
{
    private readonly struct UpdateState(World world, ComponentType transformType)
    {
        public World World { get; } = world;
        public ComponentType TransformType { get; } = transformType;
    }

    private static void UpdateRoot(in UpdateState state, EntityId entity, ref Node node)
    {
        if (node.Parent.IsNone) UpdateSubtree(state.World, state.TransformType, entity, Matrix3x2.Identity);
    }

    public void Update(World world, float dt)
    {
        _ = dt;
        var state = new UpdateState(world, world.Components.Get<Transform2D>());
        world.Query<Node>().ForEach(in state, UpdateRoot);
    }

    private static void UpdateSubtree(World world, ComponentType transformType, EntityId entity, Matrix3x2 parentWorld)
    {
        Matrix3x2 childParentWorld = parentWorld;
        if (world.Has(entity, transformType))
        {
            ref Transform2D transform = ref world.Get<Transform2D>(entity, transformType);
            transform.World = transform.LocalMatrix * parentWorld;
            childParentWorld = transform.World;
        }
        foreach (EntityId child in world.Children(entity)) UpdateSubtree(world, transformType, child, childParentWorld);
    }
}
