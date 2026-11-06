namespace XEngine.Interactive.Input;

public sealed class Axis(AxisSettings settings)
{
    public string Positive { get; } = settings.Positive;
    public string Negative { get; } = settings.Negative;
    public float Gravity { get; } = settings.Gravity;
    public float Sensitivity { get; } = settings.Sensitivity;
    public float Value { get; private set; }

    public void Update(IInputService input, float dt)
    {
        var target = 0f;
        if (input.IsActionActive(Positive)) target += 1f;
        if (input.IsActionActive(Negative)) target -= 1f;

        Value = target != 0f
            ? MoveToward(Value, target, Sensitivity * dt)
            : MoveToward(Value, 0f, Gravity * dt);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        var difference = target - current;
        return MathF.Abs(difference) <= maxDelta ? target : current + MathF.Sign(difference) * maxDelta;
    }
}
