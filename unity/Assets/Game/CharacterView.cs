using Rats.Sim;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rats.Game
{
    /// <summary>
    /// Visual for one fighter. A capsule by default; if the Mixamo placeholder is in
    /// Resources/Mixamo, the humanoid is shown and its melee clip is posed from the
    /// sim's swing progress. The animation graph never advances on its own.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        const string CharacterResource = "Mixamo/character";
        const string MeleeResource = "Mixamo/melee";

        GameObject capsule;
        PlayableGraph graph;
        AnimationClipPlayable meleePlayable;
        AnimationClip meleeClip;

        public bool HasHumanoid => meleeClip != null;

        public void Build()
        {
            capsule = Primitives.Create(PrimitiveType.Capsule, transform, new Color(0.45f, 0.2f, 0.15f));
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            TryLoadHumanoid();
        }

        public void Sync(Fighter fighter)
        {
            transform.position = SimConvert.ToUnity(fighter.Position);
            transform.rotation = Quaternion.LookRotation(SimConvert.ToUnity(fighter.Facing), Vector3.up);

            if (HasHumanoid)
            {
                meleePlayable.SetTime(fighter.SwingProgress * meleeClip.length);
                graph.Evaluate(0f);
            }
            else
            {
                // Capsule fallback: lean forward through the swing so it's visible.
                float lean = fighter.IsSwinging ? Mathf.Sin(fighter.SwingProgress * Mathf.PI) * 35f : 0f;
                capsule.transform.localRotation = Quaternion.Euler(lean, 0f, 0f);
            }
        }

        void TryLoadHumanoid()
        {
            var modelPrefab = Resources.Load<GameObject>(CharacterResource);
            meleeClip = FindClip(Resources.LoadAll<AnimationClip>(MeleeResource));
            if (modelPrefab == null || meleeClip == null)
            {
                meleeClip = null;
                return;
            }

            var model = Instantiate(modelPrefab, transform);
            model.name = "Humanoid";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;

            graph = PlayableGraph.Create("CharacterView");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            meleePlayable = AnimationClipPlayable.Create(graph, meleeClip);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(meleePlayable);
            graph.Play();

            capsule.SetActive(false);
            Debug.Log($"[CharacterView] humanoid loaded, clip '{meleeClip.name}' {meleeClip.length:F2}s, " +
                      $"humanoid avatar: {animator.avatar != null && animator.avatar.isHuman}");
        }

        static AnimationClip FindClip(AnimationClip[] clips)
        {
            foreach (var clip in clips)
                if (!clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
