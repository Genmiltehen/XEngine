using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;

namespace XEngine.Common.Transform;

internal static class TransformReparentListener
{
    private const string NotInvertibleTransformErrMsg = "Cannot preserve world transform because the new parent matrix is not invertible. Possibly some Scale factor is 0";

    public static void OnBeforeReparent(World world, in BeforeReparentEventArgs args)
    {
        ComponentHandle<Transform2D> handle = world.Handle<Transform2D>();

        if (!world.TryGet(args.Child, handle, out Transform2D childTransform)) return;
        if ((childTransform.Flags & TransformFlags.KeepWorldOnDetach) == 0) return;

        Matrix3x2 oldWorld = ComputeWorldMatrix(world, handle, args.Child);
        Matrix3x2 newParentWorld = args.NewParent.IsNone
            ? Matrix3x2.Identity
            : ComputeWorldMatrix(world, handle, args.NewParent);
        if (!Matrix3x2.Invert(newParentWorld, out Matrix3x2 inverseParent)) throw new InvalidOperationException(NotInvertibleTransformErrMsg);

        Matrix3x2 localWorld = oldWorld * inverseParent;
        Decompose(localWorld, out Vector2 position, out var rotation, out Vector2 scale);
        ref Transform2D transform = ref world.Get(args.Child, handle);
        transform.Position = position;
        transform.Rotation = rotation;
        transform.Scale = scale;
        transform.World = oldWorld;
    }

    // Считаем от локальных значений: кеш World при переподвешивании ещё старый.
    private static Matrix3x2 ComputeWorldMatrix(World world, ComponentHandle<Transform2D> handle, EntityId entity)
    {
        Matrix3x2 worldMatrix = Matrix3x2.Identity;
        EntityId current = entity;
        while (!current.IsNone)
        {
            if (world.TryGet(current, handle, out Transform2D local)) worldMatrix *= local.LocalMatrix;
            current = world.Get<Node>(current).Parent;
        }
        return worldMatrix;
    }

    private static void Decompose(Matrix3x2 matrix, out Vector2 position, out float rotation, out Vector2 scale)
    {
        position = new Vector2(matrix.M31, matrix.M32);
        var scaleX = MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12));
        if (scaleX > 1e-6f)
        {
            rotation = MathF.Atan2(matrix.M12, matrix.M11);
            var scaleY = ((matrix.M11 * matrix.M22) - (matrix.M12 * matrix.M21)) / scaleX;
            scale = new Vector2(scaleX, scaleY);
            return;
        }
        var fallbackScaleY = MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22));
        rotation = MathF.Atan2(-matrix.M21, matrix.M22);
        scale = new Vector2(0f, fallbackScaleY);
    }
}
