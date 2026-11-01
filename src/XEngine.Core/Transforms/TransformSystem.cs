using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Systems;

namespace XEngine.Core.Transforms;

public sealed class TransformSystem : ISystem
{
    private static void UpdateRoot(in World world, EntityId entity, ref Node node)
    {
        if (node.Parent.IsNone) UpdateSubtree(world, entity, Matrix3x2.Identity);
    }

    public void Update(World world, float dt)
    {
        _ = dt;
        world.Query<Node>().ForEach(world, UpdateRoot);
    }

    private static void UpdateSubtree(World world, EntityId entity, Matrix3x2 parentWorld)
    {
        Matrix3x2 childParentWorld = parentWorld;
        if (world.Has<Transform2D>(entity))
        {
            ref Transform2D transform = ref world.Get<Transform2D>(entity);
            transform.World = transform.LocalMatrix * parentWorld;
            childParentWorld = transform.World;
        }
        foreach (EntityId child in world.Children(entity)) UpdateSubtree(world, child, childParentWorld);    }
}
