using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Watches sim state for moments worth reacting to and triggers presentation: damage pop-ups (with
    /// backstabs), sparks, flinch and camera shake on hits; ticking blips before a freeze; a tone and the
    /// FROZEN flash when it lands. Reads the sim's last-hit record and phase, so host and client match.
    /// </summary>
    sealed class Feedback
    {
        static readonly int[] BlipAtTicksLeft = { 45, 30, 15 };

        readonly int[] seenHitTick = { -1, -1 };
        readonly CameraRig rig;
        readonly CharacterView[] views;
        readonly Hud hud;
        readonly Effects effects;
        readonly VisualSettings look;
        readonly AudioSource audio;
        readonly AudioClip blip, thunk, hit;
        Phase lastPhase;
        int lastTicksLeft;

        public Feedback(CameraRig rig, CharacterView[] views, Hud hud, Effects effects, VisualSettings look)
        {
            this.rig = rig;
            this.views = views;
            this.hud = hud;
            this.effects = effects;
            this.look = look;
            var cam = rig.Camera.gameObject;
            cam.AddComponent<AudioListener>();
            audio = cam.AddComponent<AudioSource>();
            blip = Tone("blip", 880f, 0.06f, 0.25f);
            thunk = Tone("thunk", 110f, 0.35f, 0.5f);
            hit = Tone("hit", 220f, 0.08f, 0.35f);
        }

        public void Sync(Simulation sim, int local)
        {
            for (int i = 0; i < 2; i++)
            {
                var f = sim.Fighters[i];
                if (f.LastHitTick == seenHitTick[i]) continue;
                seenHitTick[i] = f.LastHitTick;
                if (f.LastHitTick < 0) continue;
                var at = SimConvert.ToUnity(f.Position) + Vector3.up * 1.1f;
                if (sim.InFire(f) && !f.LastHitBackstab)
                {
                    // Fire damage: small orange numbers, no shake or flinch (it ticks 4 times a second).
                    hud.Popup(at + Vector3.up * 0.8f, f.LastHitDamage.ToString(), new Color(1f, 0.6f, 0.2f), 18);
                    continue;
                }
                hud.Popup(at + Vector3.up * 1.1f,
                    f.LastHitBackstab ? $"{f.LastHitDamage}  BACKSTAB!" : f.LastHitDamage.ToString(),
                    f.LastHitBackstab ? new Color(1f, 0.85f, 0.2f) : i == local ? new Color(1f, 0.35f, 0.3f) : Color.white,
                    f.LastHitBackstab ? 34 : 28);
                effects.Sparks(at, f.LastHitBackstab);
                views[i].Flinch();
                rig.AddShake((i == local ? 0.22f : 0.08f) * look.ShakeScale);
                audio.PlayOneShot(hit);
            }

            if (sim.Phase == Phase.Combat && lastPhase == Phase.Combat)
                foreach (int at in BlipAtTicksLeft)
                    if (lastTicksLeft > at && sim.PhaseTicksLeft <= at) audio.PlayOneShot(blip);
            if (lastPhase == Phase.Combat && sim.Phase == Phase.Freeze)
            {
                audio.PlayOneShot(thunk);
                hud.FreezeStarted();
            }
            lastPhase = sim.Phase;
            lastTicksLeft = sim.PhaseTicksLeft;
        }

        /// <summary>A short sine tone with a linear fade, generated in code (no audio files yet).</summary>
        static AudioClip Tone(string name, float frequency, float seconds, float volume)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / rate) * volume * (1f - (float)i / n);
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
