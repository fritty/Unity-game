using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MapGenerator : MonoBehaviour {

    const int threadGroupSize = 8;
    public ComputeShader mapShader;

    protected List<ComputeBuffer> buffersToRelease;

    void OnValidate() {
        if (FindObjectOfType<MeshGenerator>()) {
            FindObjectOfType<MeshGenerator>().RequestMeshUpdate();
        }
    }

    public virtual ComputeBuffer Generate (ComputeBuffer pointsBuffer) {       
        int kernelHandle = mapShader.FindKernel("MapGen");
        int threadsPerAxis = 8; 
        // Points buffer is populated inside shader with pos (xyz).
        // Set paramaters
        mapShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        mapShader.SetInt ("Width", Chunk.size.x);
        mapShader.SetFloat ("Height", Chunk.size.y);
        

        // Dispatch shader
        mapShader.Dispatch (kernelHandle, Chunk.size.x / (threadsPerAxis*4), Chunk.size.y / (threadsPerAxis), Chunk.size.z);

        if (buffersToRelease != null) {
            foreach (var b in buffersToRelease) {
                b.Release();
            }
        }

        // Return voxel data buffer so it can be used to generate mesh
        return pointsBuffer;
    }
}