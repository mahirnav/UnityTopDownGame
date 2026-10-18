using System;

namespace Rats.Sim
{
    /// <summary>Oval pit with 4 round pillars, all on the floor plane. Pure geometry, no state.</summary>
    public sealed class Arena
    {
        public readonly double RadiusX, RadiusZ, PillarRadius;
        public readonly Vec3[] Pillars;

        public Arena(Tunables t)
        {
            RadiusX = t.ArenaRadiusX;
            RadiusZ = t.ArenaRadiusZ;
            PillarRadius = t.PillarRadius;
            Pillars = new[]
            {
                new Vec3(t.PillarOffsetX, 0, t.PillarOffsetZ),
                new Vec3(-t.PillarOffsetX, 0, t.PillarOffsetZ),
                new Vec3(t.PillarOffsetX, 0, -t.PillarOffsetZ),
                new Vec3(-t.PillarOffsetX, 0, -t.PillarOffsetZ),
            };
        }

        /// <summary>True if a circle of the given radius fits inside the oval wall.</summary>
        public bool Inside(Vec3 p, double radius)
        {
            double a = RadiusX - radius, b = RadiusZ - radius;
            return (p.X * p.X) / (a * a) + (p.Z * p.Z) / (b * b) <= 1;
        }

        /// <summary>True if the point is inside the oval scaled down by the given factor (the fire's safe zone).</summary>
        public bool InsideScaled(Vec3 p, double scale)
        {
            if (scale <= 0) return false;
            double a = RadiusX * scale, b = RadiusZ * scale;
            return (p.X * p.X) / (a * a) + (p.Z * p.Z) / (b * b) <= 1;
        }

        public bool HitsPillar(Vec3 p, double radius)
        {
            foreach (var c in Pillars)
                if (Vec3.DistanceXZ(p, c) < PillarRadius + radius) return true;
            return false;
        }

        /// <summary>Pushes a fighter-sized circle out of pillars and back inside the wall.</summary>
        public Vec3 Constrain(Vec3 p, double radius)
        {
            foreach (var c in Pillars)
            {
                var d = (p - c).Flat;
                double dist = d.Length, min = PillarRadius + radius;
                if (dist >= min) continue;
                var normal = dist > 1e-6 ? d / dist : new Vec3(1, 0, 0);
                p = new Vec3(c.X + normal.X * min, p.Y, c.Z + normal.Z * min);
            }

            double a = RadiusX - radius, b = RadiusZ - radius;
            double k = (p.X * p.X) / (a * a) + (p.Z * p.Z) / (b * b);
            if (k > 1)
            {
                double s = 1 / Math.Sqrt(k);
                p = new Vec3(p.X * s, p.Y, p.Z * s);
            }
            return p;
        }

        /// <summary>False if the floor-plane segment a-b passes through a pillar.</summary>
        public bool LineOfSight(Vec3 a, Vec3 b)
        {
            var ab = (b - a).Flat;
            double lenSq = Vec3.Dot(ab, ab);
            foreach (var c in Pillars)
            {
                double t = lenSq > 0 ? Math.Clamp(Vec3.Dot((c - a).Flat, ab) / lenSq, 0, 1) : 0;
                var closest = a.Flat + ab * t;
                if (Vec3.DistanceXZ(closest, c) < PillarRadius) return false;
            }
            return true;
        }
    }
}
