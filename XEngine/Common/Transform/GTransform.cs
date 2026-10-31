using OpenTK.Mathematics;
using XEngine.Core.Base;
using XEngine.Core.Utils;

namespace XEngine.Core.Common.Transform
{
    public sealed class GTransform : GameComponent
    {
        private Vector3 _position = Vector3.Zero;
        private float _rotation = 0;
        private Matrix4 _cachedMatrix;
        private bool _isDirty = true;

        public GTransform? Parent { get; private set; } = null;
        public GTransform? FirstChild { get; private set; } = null;
        public GTransform? NextSibling { get; private set; } = null;
        public GTransform? PrevSibling { get; private set; } = null;

        public bool IsSynced { get; private set; } = true;

        public GTransform Init(TransformValues values) => Init(values.Position, values.Rotation);

        public GTransform Init(Vector3 pos, float rotation)
        {
            _position = pos;
            _rotation = rotation;
            IsSynced = false;
            return this;
        }

        public void SetParent(GTransform? newParent)
        {

            if (Parent != null)
            {
                if (Parent.FirstChild == this) Parent.FirstChild = NextSibling;
                if (PrevSibling != null) PrevSibling.NextSibling = NextSibling;
                if (NextSibling != null) NextSibling.PrevSibling = PrevSibling;
            }
            Parent = newParent;
            if (Parent != null)
            {
                NextSibling = Parent.FirstChild;
                if (NextSibling != null) NextSibling.PrevSibling = this;
                Parent.FirstChild = this;
                PrevSibling = null;
            }
        }

        public GTransform? GetChild(int index)
        {
            GTransform? current = FirstChild;
            int count = 0;
            while (current != null)
            {
                if (count == index) return current;
                count++;
                current = current.NextSibling;
            }
            throw new IndexOutOfRangeException();
        }

        public void SetDirty()
        {
            if (_isDirty) return;
            _isDirty = true;

            GTransform? current = FirstChild;
            while (current != null)
            {
                current.SetDirty();
                current = current.NextSibling;
            }
        }

        public Vector2 Position2D
        {
            get => new(_position.X, _position.Y);
            set => Position = new Vector3(value.X, value.Y, _position.Z);
        }

        public float Layer
        {
            get => _position.Z;
            set => Position = new Vector3(_position.X, _position.Y, value);
        }

        public Vector3 RelativePosition => Parent != null ? (MathUtils.Homogenize(_position) * Parent.GetWorldMatrix()).Xyz : Position;
        public Vector2 RelativePosition2D => Parent != null ? (MathUtils.Homogenize(_position) * Parent.GetWorldMatrix()).Xy : Position2D;

        public Vector3 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    SetDirty();
                    IsSynced = false;
                }
            }
        }

        public void Approach(Vector3 newPos, float strength)
        {
            Position = Vector3.Lerp(Position, newPos, strength);
        }

        public float Rotation
        {
            get => _rotation;
            set
            {
                if (_rotation != value)
                {
                    _rotation = value;
                    SetDirty();
                    IsSynced = false;
                }
            }
        }

        public Matrix4 GetWorldMatrix()
        {
            if (_isDirty)
            {
                _cachedMatrix = Matrix4.CreateRotationZ(_rotation) *
                                Matrix4.CreateTranslation(_position);
                if (Parent != null) _cachedMatrix *= Parent.GetWorldMatrix();
                _isDirty = false;
            }
            return _cachedMatrix;
        }

        public override string ToString()
        {
            return $"[{_position}] ({_rotation:F2}rad)";
        }
    }
}
