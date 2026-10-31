using OpenTK.Mathematics;

namespace XEngine.Core.Utils
{
    public static class MathUtils
    {
        public const float Epsilon = 1e-6f;

        public static Vector2 FromPolar(float rho, float theta)
        {
            var (s, c) = MathF.SinCos(theta);
            return rho * new Vector2(c, s);
        }

        public static Vector2 Rotate(Vector2 v, float angle)
        {
            var (s, c) = MathF.SinCos(angle);
            return new Vector2(c * v.X + s * v.Y, -s * v.X + c * v.Y);
        }

        public static Vector4 Homogenize(Vector3 v) => new(v.X, v.Y, v.X, 1);

        public static Vector3 Dehomogenize(Vector4 v) => v.Xyz / v.W;

        public static float LimitedStep(float from, float to, float step)
        {
            float diff = to - from;
            return Math.Abs(diff) <= step ? diff : Math.Sign(diff) * step;
        }

        public static Vector2 LimitedStep(Vector2 from, Vector2 to, float step)
        {
            Vector2 diff = to - from;
            return diff.LengthSquared <= step * step ? diff : diff.Normalized() * step;
        }

        public static float MoveToward(float from, float to, float step)
        {
            if (Math.Abs(to - from) <= step)
                return to;

            return from + Math.Sign(to - from) * step;
        }
    }
}
