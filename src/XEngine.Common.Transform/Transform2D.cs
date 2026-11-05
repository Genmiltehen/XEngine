using System.Numerics;
using XEngine.Core.Ecs;

namespace XEngine.Common.Transform;

public struct Transform2D : IComponent
{
    public Vector2 Position;
    public float Rotation;
    public Vector2 Scale;
    public TransformFlags Flags;

    // Строка-вектор: World = Local * Parent.World, Local = Scale * Rotation * Translation.
    public Matrix3x2 World;

    public static Transform2D Identity => new()
    {
        Position = Vector2.Zero,
        Rotation = 0f,
        Scale = Vector2.One,
        Flags = TransformFlags.None,
        World = Matrix3x2.Identity,
    };

    public static Transform2D At(Vector2 position) => new()
    {
        Position = position,
        Rotation = 0f,
        Scale = Vector2.One,
        Flags = TransformFlags.None,
        World = Matrix3x2.CreateTranslation(position),
    };

    public readonly Matrix3x2 LocalMatrix =>
        Matrix3x2.CreateScale(Scale) *
        Matrix3x2.CreateRotation(Rotation) *
        Matrix3x2.CreateTranslation(Position);
}
