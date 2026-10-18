using System.Collections.Generic;
using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Presentation-only feedback driven by sim state: damage pop-ups (with backstabs), flinch and camera
    /// shake on hits, and the turn-end cue (tint, shrinking bar, ticking blips, "FROZEN" + low tone).
    /// Reads the sim's last-hit record and phase, so host and client see the same thing. Never feeds back.
    /// </summary>
    sealed class Feedback
    {
        const int CueTicks = 60;                       // warn during the last second of a turn
        static readonly int[] BlipAtTicksLeft = { 45, 30, 15 };
        static readonly Color CueColor = new Color(0.55f, 0.8f, 1f);
        const float PopupSeconds = 1f;

        struct Popup { public Vector3 World; public string Text; public float Born; public Color Color; public int Size; }

        readonly List<Popup> popups = new List<Popup>();
        readonly int[] seenHitTick = { -1, -1 };
        readonly CameraRig rig;
        readonly CharacterView[] views;
        readonly AudioSource audio;
        readonly AudioClip blip, thunk;
        Phase lastPhase;
        int lastTicksLeft;
        float frozeAt = -10f;

        public Feedback(CameraRig rig, CharacterView[] views)
        {
            this.rig = rig;
            this.views = views;
            var cam = rig.Camera.gameObject;
            cam.AddComponent<AudioListener>();
            audio = cam.AddComponent<AudioSource>();
            blip = Tone("blip", 880f, 0.06f, 0.25f);
            thunk = Tone("thunk", 110f, 0.35f, 0.5f);
        }

        public void Sync(Simulation sim, int local)
        {
            for (int i = 0; i < 2; i++)
            {
                var f = sim.Fighters[i];
                if (f.LastHitTick == seenHitTick[i]) continue;
                seenHitTick[i] = f.LastHitTick;
                if (f.LastHitTick < 0) continue;
                popups.Add(new Popup
                {
                    World = SimConvert.ToUnity(f.Position) + Vector3.up * 2.2f,
                    Text = f.LastHitBackstab ? $"{f.LastHitDamage}  BACKSTAB!" : f.LastHitDamage.ToString(),
                    Born = Time.unscaledTime,
                    Color = f.LastHitBackstab ? new Color(1f, 0.85f, 0.2f) : i == local ? new Color(1f, 0.35f, 0.3f) : Color.white,
                    Size = f.LastHitBackstab ? 34 : 28,
                });
                views[i].Flinch();
                rig.AddShake(i == local ? 0.22f : 0.08f);
            }

            if (sim.Phase == Phase.Combat && lastPhase == Phase.Combat)
                foreach (int at in BlipAtTicksLeft)
                    if (lastTicksLeft > at && sim.PhaseTicksLeft <= at) audio.PlayOneShot(blip);
            if (lastPhase == Phase.Combat && sim.Phase == Phase.Freeze)
            {
                audio.PlayOneShot(thunk);
                frozeAt = Time.unscaledTime;
            }
            lastPhase = sim.Phase;
            lastTicksLeft = sim.PhaseTicksLeft;
        }

        public void Draw(Simulation sim, Camera cam)
        {
            if (sim.Phase == Phase.Combat && sim.PhaseTicksLeft <= CueTicks)
            {
                float left = sim.PhaseTicksLeft / (float)CueTicks;
                GUI.color = new Color(CueColor.r, CueColor.g, CueColor.b, 0.22f * (1f - left));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = new Color(CueColor.r, CueColor.g, CueColor.b, 0.9f);
                float barWidth = Screen.width * 0.4f * left;
                GUI.DrawTexture(new Rect((Screen.width - barWidth) / 2, 36, barWidth, 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            float sinceFreeze = Time.unscaledTime - frozeAt;
            if (sinceFreeze < 0.7f)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(CueColor.r, CueColor.g, CueColor.b, 1f - sinceFreeze / 0.7f);
                GUI.Label(new Rect(0, Screen.height * 0.22f, Screen.width, 70), "FROZEN", style);
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.unscaledTime - p.Born;
                if (age > PopupSeconds) { popups.RemoveAt(i); continue; }
                var screen = cam.WorldToScreenPoint(p.World + Vector3.up * age * 0.8f);
                if (screen.z < 0) continue;
                var style = new GUIStyle(GUI.skin.label) { fontSize = p.Size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(p.Color.r, p.Color.g, p.Color.b, 1f - age / PopupSeconds);
                GUI.Label(new Rect(screen.x - 150, Screen.height - screen.y - 20, 300, 40), p.Text, style);
            }
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
