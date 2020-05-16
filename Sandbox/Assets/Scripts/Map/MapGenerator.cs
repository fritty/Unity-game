using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour {
    public float noiseScale = Chunk.size.height;
    public float noiseFrequency = 0.5f;
    public ComputeShader mapShader;

    //protected List<ComputeBuffer> buffersToRelease;

    void OnValidate() {
        if (FindObjectOfType<MeshGenerator>()) {
            FindObjectOfType<MeshGenerator>().RequestMeshUpdate();
        }
    }

    public void Generate (ComputeBuffer pointsBuffer, Vector3 origin) {
         CpuGen(pointsBuffer, origin);
    }

    void CpuGen (ComputeBuffer pointsBuffer, Vector3 origin){
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
        //float[,] noiseMap = new float[Chunk.size.width, Chunk.size.width];
        float noiseMap;

        for (int i = 0; i < Chunk.size.width; i++){
            for (int j = 0; j < Chunk.size.width; j++){
                noiseMap = noiseScale * Mathf.PerlinNoise((i + origin.x) * noiseFrequency + 500, (j + origin.z) * noiseFrequency + 500);
                for (int k = 0; k < Chunk.size.height; k++){
                    if (k == Mathf.FloorToInt(noiseMap)){
                        blocks[j,k,i] = (byte)Mathf.RoundToInt(255 * (noiseMap - Mathf.Floor(noiseMap)));
                        continue;
                    }

                    if (k > noiseMap){
                        blocks[j,k,i] = 0;                        
                    }
                    else {
                        blocks[j,k,i] = 255;                        
                    }
                }            
            }              
        }

        pointsBuffer.SetData(blocks);
    }

    void DispatchShader (ComputeBuffer pointsBuffer){
        int kernelHandle = mapShader.FindKernel("MapGen");
        int threadsPerAxis = 8; 
        // Points buffer is populated inside shader with pos (xyz).
        // Set paramaters
        mapShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        mapShader.SetInt ("Width", Chunk.size.width);
        mapShader.SetFloat ("Height", Chunk.size.height);  

        // Dispatch shader
        mapShader.Dispatch (kernelHandle, Chunk.size.width / (threadsPerAxis*4), Chunk.size.height / (threadsPerAxis), Chunk.size.width);
    }
}