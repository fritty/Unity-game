using UnityEngine;

[CreateAssetMenu()]
public class MeshGeneratorSettings : ScriptableObject
{
    [Header("Mesh properties")]
    public Material material;
    public bool generateColliders;

    [Header("Gpu generation(outdated)")]
    public bool useGpu = false;
    [Tooltip("Shader used for generation")]
    public ComputeShader marchShader;

    [Tooltip("Determines how many operations will be done per frame")]
    [Range(1, 60)]
    public int targetFps = 30;    
    [Tooltip("Log number of chunks generated per frame")]              
    public bool log = false;
}
