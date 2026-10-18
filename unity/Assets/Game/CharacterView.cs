using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Capsule visual for one fighter, posed entirely from sim state. Color shows the frame-data
    /// phase of the current action (startup yellow, active red, recovery dark), which makes
    /// timing readable while tuning. Pips above the head show remaining action points.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        static readonly Color StartupColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color ActiveColor = new Color(1f, 0.15f, 0.1f);
        static readonly Color RecoveryColor = new Color(0.25f, 0.22f, 0.2f);
        static readonly Color PipOn = new Color(1f, 0.8f, 0.2f);
        static readonly Color PipOff = new Color(0.2f, 0.2f, 0.2f);
        const float HitFlashSeconds = 0.12f;

        Transform body;
        Transform pipRoot;
        Renderer bodyRenderer;
        Renderer[] pips;
        Color baseColor;
        int lastHp = int.MaxValue;
        float flashUntil;

        public void Build(Color color, int pipCount)
        {
            baseColor = color;
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            var capsule = Primitives.Create(PrimitiveType.Capsule, body, color);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            capsule.transform.localScale = new Vector3(0.8f, 1f, 0.8f); // 0.4 m radius, matches FighterRadius
            bodyRenderer = capsule.GetComponent<Renderer>();
            var nose = Primitives.Create(PrimitiveType.Cube, body, Color.black);
            nose.transform.localPosition = new Vector3(0f, 1.5f, 0.4f);
            nose.transform.localScale = new Vector3(0.15f, 0.12f, 0.25f);

            pipRoot = new GameObject("Pips").transform;
            pipRoot.SetParent(transform, false);
            pipRoot.localPosition = new Vector3(0f, 2.4f, 0f);
            pips = new Renderer[pipCount];
            for (int i = 0; i < pipCount; i++)
            {
                var pip = Primitives.Create(PrimitiveType.Sphere, pipRoot, PipOn);
                pip.transform.localPosition = new Vector3((i - (pipCount - 1) * 0.5f) * 0.3f, 0f, 0f);
                pip.transform.localScale = Vector3.one * 0.18f;
                pips[i] = pip.GetComponent<Renderer>();
            }
        }

        public void Sync(Fighter fighter, Tunables t, Quaternion cameraRotation)
        {
            transform.position = SimConvert.ToUnity(fighter.Position);
            body.rotation = Quaternion.LookRotation(SimConvert.ToUnity(fighter.Facing), Vector3.up);

            float lean = 0f;
            var color = baseColor;
            if (fighter.IsBusy)
            {
                var def = fighter.Frames; // locked when the action started
                int tick = fighter.ActionTick;
                color = tick < def.Startup ? StartupColor : def.IsActive(tick) ? ActiveColor : RecoveryColor;
                float progress = (float)tick / def.Total;
                lean = fighter.Action == ActionKind.Dash ? 25f
                     : fighter.Action == ActionKind.Melee ? Mathf.Sin(progress * Mathf.PI) * 35f
                     : 0f;
            }
            body.localRotation *= Quaternion.Euler(lean, 0f, 0f);

            if (fighter.Hp < lastHp && lastHp != int.MaxValue) flashUntil = Time.unscaledTime + HitFlashSeconds;
            lastHp = fighter.Hp;
            if (Time.unscaledTime < flashUntil) color = Color.white; // visual only, never feeds back into the sim
            bodyRenderer.sharedMaterial.color = color;

            pipRoot.rotation = cameraRotation; // pips line up across the screen
            for (int i = 0; i < pips.Length; i++)
                pips[i].sharedMaterial.color = i < fighter.ActionPoints ? PipOn : PipOff;
        }
    }
}
