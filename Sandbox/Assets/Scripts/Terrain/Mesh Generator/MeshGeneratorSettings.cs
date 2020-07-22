using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "Terrain/MeshGeneratorSettings")]
    public class MeshGeneratorSettings : ScriptableObject
    {
        public Material Material;
        [ConditionalHide("ColoredMaterial")]
        public Gradient ColorGradient;
        public bool GenerateColliders;

        [HideInInspector]
        public bool ColoredMaterial = false;
        [HideInInspector]
        public bool IsChanged = false;

        private void Awake()
        {
            ColoredMaterial = Material.name.Contains("Colored");
        }

        private void OnValidate()
        {
            ColoredMaterial = Material.name.Contains("Colored");
            IsChanged = true;
        }
    }
}