namespace XEngine.Interactive.Input;

public sealed class AxisSettings
{
    public string Positive { get; set; } = "";
    public string Negative { get; set; } = "";
    public float Sensitivity { get; set; } = 3f;
    public float Gravity { get; set; } = 3f;
}
