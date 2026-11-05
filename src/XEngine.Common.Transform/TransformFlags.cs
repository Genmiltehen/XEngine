namespace XEngine.Common.Transform;

[Flags]
public enum TransformFlags
{
    None = 0,
    KeepWorldOnDetach = 1 << 0,
}
