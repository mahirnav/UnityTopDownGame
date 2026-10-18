using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Third-person camera for one fighter. Locked on: sits behind the fighter on the line from the
    /// opponent, framing both. Free: orbits behind with Q/E. Stays inside the arena walls. Purely visual.
    /// </summary>
    public sealed class CameraRig
    {
        // Shoulder offset keeps your own capsule from hiding the opponent and your projectiles.
        const float Distance = 5.5f, Height = 2.6f, Shoulder = 1.3f, FollowRate = 8f, OrbitDegreesPerSecond = 120f;

        public readonly Camera Camera;
        float yaw;
        float shake;
        Vector3 smoothPosition;
        bool hasPosition;

        /// <summary>Kick the camera; decays over a fraction of a second (visual only).</summary>
        public void AddShake(float amount) => shake = Mathf.Max(shake, amount);

        readonly float wallX, wallZ;

        /// <summary>The arena's oval radii keep the camera inside the (tall) walls.</summary>
        public CameraRig(Camera camera, Arena arena)
        {
            Camera = camera;
            wallX = (float)arena.RadiusX - 0.9f;
            wallZ = (float)arena.RadiusZ - 0.9f;
        }

        public void Update(Fighter me, Fighter target, float deltaTime, bool snap = false)
        {
            var self = SimConvert.ToUnity(me.Position);
            var other = SimConvert.ToUnity(target.Position);
            Vector3 desired, lookAt;
            if (me.LockedOn && (other - self).sqrMagnitude > 0.01f)
            {
                var back = Vector3.ProjectOnPlane(self - other, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, -back);
                desired = self + back * Distance + right * Shoulder + Vector3.up * Height;
                lookAt = Vector3.Lerp(self + Vector3.up * 1.4f, other + Vector3.up * 1f, 0.35f) + right * (Shoulder * 0.5f);
                yaw = Mathf.Atan2(-back.x, -back.z) * Mathf.Rad2Deg;
            }
            else
            {
                yaw += ((Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f)) * OrbitDegreesPerSecond * deltaTime;
                var forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                desired = self - forward * Distance + right * Shoulder + Vector3.up * Height;
                lookAt = self + Vector3.up * 1.5f + forward * 2f + right * (Shoulder * 0.5f);
            }

            desired = InsideWalls(self + Vector3.up * Height, desired);
            var t = Camera.transform;
            float blend = snap ? 1f : 1f - Mathf.Exp(-FollowRate * deltaTime);
            if (!hasPosition) { smoothPosition = t.position; hasPosition = true; }
            smoothPosition = Vector3.Lerp(smoothPosition, desired, blend);
            shake = Mathf.Max(0f, shake - deltaTime * 1.2f);
            t.position = smoothPosition + (shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero);
            var look = Quaternion.LookRotation(lookAt - smoothPosition, Vector3.up);
            // Set exactly once on target: slerping a rotation onto itself wobbles by a hair, which the low-res effect turns into pixel flicker.
            t.rotation = snap || Quaternion.Angle(t.rotation, look) < 0.05f ? look : Quaternion.Slerp(t.rotation, look, blend);
        }

        /// <summary>If the camera would end up behind the wall, pull it in along the line toward the fighter.</summary>
        Vector3 InsideWalls(Vector3 pivot, Vector3 desired)
        {
            if (Inside(desired)) return desired;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 12; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Inside(Vector3.Lerp(pivot, desired, mid))) lo = mid; else hi = mid;
            }
            return Vector3.Lerp(pivot, desired, lo) + Vector3.up * (1f - lo) * 1.2f; // rise a little when pulled in
        }

        bool Inside(Vector3 p) => (p.x * p.x) / (wallX * wallX) + (p.z * p.z) / (wallZ * wallZ) <= 1f;
    }
}
