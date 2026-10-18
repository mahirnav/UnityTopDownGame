using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    static class Primitives
    {
        /// <summary>Unity primitive with its collider removed: gameplay never uses engine physics.</summary>
        public static GameObject Create(PrimitiveType type, Transform parent, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = color };
            return go;
        }

        /// <summary>Same, sharing one material (arena pieces use a handful of materials between them).</summary>
        public static GameObject Create(PrimitiveType type, Transform parent, Material shared)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = shared;
            return go;
        }
    }

    static class SimConvert
    {
        public static Vector3 ToUnity(Vec3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
