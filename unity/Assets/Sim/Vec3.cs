using System;

namespace Rats.Sim
{
    /// <summary>Minimal float vector so the sim has no UnityEngine dependency.</summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X, Y, Z;

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);
        public static readonly Vec3 Forward = new Vec3(0f, 0f, 1f);

        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalized
        {
            get { var len = Length; return len > 0f ? this / len : Zero; }
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        /// <summary>Bit-exact comparison. Tests rely on this, so no epsilon.</summary>
        public bool Equals(Vec3 o) => X.Equals(o.X) && Y.Equals(o.Y) && Z.Equals(o.Z);
        public override bool Equals(object obj) => obj is Vec3 o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        public override string ToString() => $"({X:R}, {Y:R}, {Z:R})";
    }
}
