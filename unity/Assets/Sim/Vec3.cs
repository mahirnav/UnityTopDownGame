using System;

namespace Rats.Sim
{
    /// <summary>
    /// Minimal vector so the sim has no UnityEngine dependency. Double, not float: Unity's Mono keeps
    /// float intermediates at higher precision than .NET does, so float math gave different results
    /// in the game and in the tests (replays diverged). IEEE double math matches on both.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly double X, Y, Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 Up = new Vec3(0, 1, 0);
        public static readonly Vec3 Forward = new Vec3(0, 0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalized
        {
            get { var len = Length; return len > 0 ? this / len : Zero; }
        }

        /// <summary>Same vector on the floor plane (Y = 0).</summary>
        public Vec3 Flat => new Vec3(X, 0, Z);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static double DistanceXZ(Vec3 a, Vec3 b) => (a - b).Flat.Length;

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, double s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        /// <summary>Bit-exact comparison. Tests rely on this, so no epsilon.</summary>
        public bool Equals(Vec3 o) => X.Equals(o.X) && Y.Equals(o.Y) && Z.Equals(o.Z);
        public override bool Equals(object obj) => obj is Vec3 o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        public override string ToString() => $"({X:R}, {Y:R}, {Z:R})";
    }
}
