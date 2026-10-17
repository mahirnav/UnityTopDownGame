using System;

namespace Rats.Sim
{
    /// <summary>Oval pit with 4 round pillars, all on the floor plane. Pure geometry, no state.</summary>
    public sealed class Arena
    {
        public readonly float RadiusX, RadiusZ, PillarRadius;
        public readonly Vec3[] Pillars;

        public Arena(Tunables t)
        {
            RadiusX = t.ArenaRadiusX;
            RadiusZ = t.ArenaRadiusZ;
            PillarRadius = t.PillarRadius;
            Pillars = new[]
            {
                new Vec3(t.PillarOffsetX, 0f, t.PillarOffsetZ),
                new Vec3(-t.PillarOffsetX, 0f, t.PillarOffsetZ),
                new Vec3(t.PillarOffsetX, 0f, -t.PillarOffsetZ),
                new Vec3(-t.PillarOffsetX, 0f, -t.PillarOffsetZ),
            };
        }

        /// <summary>True if a circle of the given radius fits inside the oval wall.</summary>
        public bool Inside(Vec3 p, float radius)
        {
            float a = RadiusX - radius, b = RadiusZ - radius;
            return (p.X * p.X) / (a * a) + (p.Z * p.Z) / (b * b) <= 1f;
        }

        public bool HitsPillar(Vec3 p, float radius)
        {
            foreach (var c in Pillars)
                if (Vec3.DistanceXZ(p, c) < PillarRadius + radius) return true;
            return false;
        }

        /// <summary>Pushes a fighter-sized circle out of pillars and back inside the wall.</summary>
        public Vec3 Constrain(Vec3 p, float radius)
        {
            foreach (var c in Pillars)
            {
                var d = (p - c).Flat;
                float dist = d.Length, min = PillarRadius + radius;
                if (dist >= min) continue;
                var normal = dist > 1e-6f ? d / dist : new Vec3(1f, 0f, 0f);
                p = new Vec3(c.X + normal.X * min, p.Y, c.Z + normal.Z * min);
            }

            float a = RadiusX - radius, b = RadiusZ - radius;
            float k = (p.X * p.X) / (a * a) + (p.Z * p.Z) / (b * b);
            if (k > 1f)
            {
                float s = 1f / MathF.Sqrt(k);
                p = new Vec3(p.X * s, p.Y, p.Z * s);
            }
            return p;
        }

        /// <summary>False if the floor-plane segment a-b passes through a pillar.</summary>
        public bool LineOfSight(Vec3 a, Vec3 b)
        {
            var ab = (b - a).Flat;
            float lenSq = Vec3.Dot(ab, ab);
            foreach (var c in Pillars)
            {
                float t = lenSq > 0f ? Math.Clamp(Vec3.Dot((c - a).Flat, ab) / lenSq, 0f, 1f) : 0f;
                var closest = a.Flat + ab * t;
                if (Vec3.DistanceXZ(closest, c) < PillarRadius) return false;
            }
            return true;
        }
    }
}
