using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class BlocksGeneratorSettings : ScriptableObject
{
    [Header("HeightMap Settings")]
    [Range(0, 32)]
    public float baseHeight = 1f;
    public HeightMapOptions UsedHeightMap; 
    
    [Space]
    [Range(0, 1)]
    public float flatTiltX;
    [Range(0, 1)]
    public float flatTiltZ;
    [Space]
    public float sinScale = Chunk.size.height;
    [Range(.005f, .1f)]
    public float sinFrequency = 0.025f;
    [Space]
    public float perlinScale = Chunk.size.height;
    [Range(.005f, .1f)]
    public float perlinFrequency = 0.025f;
    public Vector2 perlinOffset = new Vector2(0, 0);
}

public enum HeightMapOptions
{
    Flat,
    Sin,
    Perlin,
}
