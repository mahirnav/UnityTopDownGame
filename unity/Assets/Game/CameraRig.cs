using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Third-person camera for one fighter. Locked on: sits behind the fighter on the line from the
    /// opponent, framing both. Free: orbits behind with Q/E. Purely visual; reads sim state only.
    /// </summary>
    public sealed class CameraRig
    {
        // Shoulder offset keeps your own capsule from hiding the opponent and your projectiles.
        const float Distance = 5.5f, Height = 2.6f, Shoulder = 1.3f, FollowRate = 8f, OrbitDegreesPerSecond = 120f;

        public readonly Camera Camera;
        float yaw;

        public CameraRig(Camera camera) { Camera = camera; }

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

            var t = Camera.transform;
            float blend = snap ? 1f : 1f - Mathf.Exp(-FollowRate * deltaTime);
            t.position = Vector3.Lerp(t.position, desired, blend);
            var look = Quaternion.LookRotation(lookAt - t.position, Vector3.up);
            t.rotation = snap ? look : Quaternion.Slerp(t.rotation, look, blend);
        }
    }
}
