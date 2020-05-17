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
         //FlatGen(pointsBuffer, origin);
         //GradGen(pointsBuffer, origin);
         PerlinGen(pointsBuffer, origin);
    }

    void FlatGen (ComputeBuffer pointsBuffer, Vector3 origin){
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
        Vector3 tilt = new Vector3((origin.x/32 + 120)/256,0,0);

         for (byte x = 0; x < Chunk.size.width; x++){
            for (byte z = 0; z < Chunk.size.width; z++){                
                for (byte y = 0; y < Chunk.size.height; y++){
                    float val = tilt.x*x + tilt.z*z;
                    if (y == Mathf.Floor(val)){
                        blocks[z,y,x] = (byte)Mathf.RoundToInt(255 * (val - Mathf.Floor(val)));
                        continue;
                    }
                    if (y > val)                   
                        blocks[z,y,x] = 0;
                    else
                        blocks[z,y,x] = 255;
                }
            }
         }
         pointsBuffer.SetData(blocks);
    }

    void GradGen (ComputeBuffer pointsBuffer, Vector3 origin) {
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];

         for (byte x = 0; x < Chunk.size.width; x++){
            for (byte z = 0; z < Chunk.size.width; z++){                
                for (byte y = 0; y < Chunk.size.height; y++){ 
                    float xAbs = origin.x + x;
                    float zAbs = origin.z + z;
                    float val = (noiseScale-2) * Mathf.Pow(Mathf.Sin(xAbs*noiseFrequency),2);
                    if (y == Mathf.Floor(val)){
                        blocks[z,y,x] = (byte)Mathf.RoundToInt(255 * (val - Mathf.Floor(val)));
                        continue;
                    }
                    if (y > val)//Mathf.Sin((xAbs*xAbs + zAbs*zAbs)*noiseFrequency))                   
                        blocks[z,y,x] = 0;
                    else
                        blocks[z,y,x] = 255;
                }
            }
         }
         pointsBuffer.SetData(blocks);        
    }

    void PerlinGen (ComputeBuffer pointsBuffer, Vector3 origin){
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
        //float[,] noiseMap = new float[Chunk.size.width, Chunk.size.width];
        float noiseMap;

        for (byte x = 0; x < Chunk.size.width; x++){
            for (byte z = 0; z < Chunk.size.width; z++){
                noiseMap = noiseScale * Mathf.PerlinNoise((x + origin.x) * noiseFrequency + 500, (z + origin.z) * noiseFrequency + 500);
                for (byte y = 0; y < Chunk.size.height; y++){
                    if (y == Mathf.Floor(noiseMap)){
                        blocks[z,y,x] = (byte)Mathf.RoundToInt(255 * (noiseMap - Mathf.Floor(noiseMap)));
                        continue;
                    }

                    if (y > noiseMap){
                        blocks[z,y,x] = 0;                        
                    }
                    else {
                        blocks[z,y,x] = 255;                        
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