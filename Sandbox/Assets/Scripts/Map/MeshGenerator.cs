using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/* Handles mesh generation */
public class MeshGenerator : MonoBehaviour, IGenerator {
  
    [Header ("Mesh properties")]
    public Material mat;
    public bool generateColliders;

    public ComputeShader marchShader; // Shader for mesh generation

    public float targetFps = 60;

    public bool log = false;


    // Buffers
    ComputeBuffer triangleBuffer; 
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer edgeBuffer;

    Queue<Vector3Int> requestedCoords = new Queue<Vector3Int>();
 

    // Set up from map
    Action<GeneratedDataInfo<MeshData>> meshCallback;
    Map map;


    /* Interface */
    public void ManageRequests () {
        float dTime = Time.deltaTime;
        int count = 0; // number of chunks generated per frame
        bool repeat = true;
        
        while (repeat) {
            float shaderTime = Time.realtimeSinceStartup;
            bool generated = false;

            // Go through requested coordinates and generate if still relevant
            if (requestedCoords.Count > 0) {
                Vector3Int viewerCoord = new Vector3Int(Mathf.FloorToInt(map.viewer.position.x / Chunk.size.width), 0, Mathf.FloorToInt(map.viewer.position.z / Chunk.size.width));
                Vector3Int requestedCoord = requestedCoords.Dequeue();
                
                // skip outdated coordinates
                while ((Mathf.Abs(requestedCoord.x - viewerCoord.x) > map.viewDistance || Mathf.Abs(requestedCoord.z - viewerCoord.z) > map.viewDistance) && requestedCoords.Count > 0) {
                    requestedCoord = requestedCoords.Dequeue();
                }
                
                if (Mathf.Abs(requestedCoord.x - viewerCoord.x) <= map.viewDistance && Mathf.Abs(requestedCoord.z - viewerCoord.z) <= map.viewDistance) {
                    Chunk chunk;
                    Chunk chunkX;
                    Chunk chunkZ;
                    Chunk chunkC;
                    if (map.existingChunks.TryGetValue(requestedCoord,out chunk) && map.existingChunks.TryGetValue(requestedCoord + Vector3Int.right,out chunkX) &&
                        map.existingChunks.TryGetValue(requestedCoord + new Vector3Int(0,0,1),out chunkZ) && map.existingChunks.TryGetValue(requestedCoord + Vector3Int.one - Vector3Int.up,out chunkC))
                    {
                        GenerateChunkMesh(chunk, chunkX, chunkZ, chunkC);                    
                        
                        // Return requested data
                        meshCallback(new GeneratedDataInfo<MeshData>(CopyMeshData(), requestedCoord));

                        if (log) { // log number of chunks generated per frame             
                            count++;
                            Debug.Log(count);
                        }
                        generated = true;
                    }
                }   
            }

            if (!generated)            
                repeat = false; // no more requests

            // estimate time required for generation and stop if it exceedes framerate
            shaderTime = Time.realtimeSinceStartup - shaderTime;
            dTime += shaderTime;
            if (dTime + shaderTime > 1/targetFps)
                repeat = false; // no more time
        }
    }

    public void RequestData (Vector3Int coord) {
		requestedCoords.Enqueue(coord);
	}

    public void SetCallback (Action<GeneratedDataInfo<MeshData>> callback) {
        meshCallback = callback;
    }

    public void SetMapReference (Map map) {
        this.map = map;
    }


    /////////////////////
    /* Mesh generation */
    /////////////////////

    /* Generate chunk mesh based on its blocks */
    void GenerateChunkMesh (Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC) {  
        
        CreateBuffers ();
        
        int kernelHandle = marchShader.FindKernel("March");
        
        pointsBuffer.SetData(chunk.blocks); // copy blocks data
        GenerateEdgeBuffer(chunkX, chunkZ, chunkC); // get edge points        
        DispatchMarchShader(kernelHandle, chunk.coord); // compute mesh
    }  

    /* Copy data from shader output */
    MeshData CopyMeshData ()
    {  
        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount (triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData (triCountArray);
        int numTris = triCountArray[0];

        if (numTris > 0)
        {
            MeshData meshData = new MeshData(numTris, mat, generateColliders);
            // Get triangle data from shader

            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData (tris, 0, 0, numTris);         

            // add new mesh data
            for (int i = 0; i < numTris; i++) {
                meshData.AddTriangle(tris[i]);
            }            

            return meshData;
        }
        return null;
    }

    /* Execute mesh generation on GPU */
    void DispatchMarchShader (int kernelHandle, Vector3Int coord)
    {
        uint threadGroupsX;
        uint threadGroupsY;
        uint threadGroupsZ;     
        
        marchShader.GetKernelThreadGroupSizes(kernelHandle, out threadGroupsX, out threadGroupsY, out threadGroupsZ);

        triangleBuffer.SetCounterValue (0);
       
        threadGroupsX = (uint)Mathf.CeilToInt ((Chunk.size.width) / (float)threadGroupsX);
        threadGroupsZ = (uint)Mathf.CeilToInt ((Chunk.size.width) / (float)threadGroupsZ);      
        threadGroupsY = (uint)Mathf.CeilToInt ((Chunk.size.height) / (float)threadGroupsY);  

        marchShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        marchShader.SetBuffer (kernelHandle, "edge", edgeBuffer);
        marchShader.SetBuffer (kernelHandle, "triangles", triangleBuffer);
        marchShader.SetInt ("Width", Chunk.size.width);
        marchShader.SetInt ("Height", Chunk.size.height);
        //marchShader.SetVector ("Origin", OriginFromCoord(coord));
            
        marchShader.Dispatch (kernelHandle, (int)threadGroupsX, (int)threadGroupsY, (int)threadGroupsZ);
    }

    /* Copies edge data from adjacent chunks */
    void GenerateEdgeBuffer (Chunk chunkX, Chunk chunkZ, Chunk chunkC)
    {
        Vector3Int xEdge = new Vector3Int(1,0,0);
        Vector3Int zEdge = new Vector3Int(0,0,1);
        
        byte[] edgeArray = new byte[(Chunk.size.width + Chunk.size.width + 1) * Chunk.size.height]; // x*y(zEdge) + z*y(xEdge) + y(Corner)
        
        for (int j = 0; j < Chunk.size.height; j++)
            for (int i = 0; i < Chunk.size.width; i++)
            {
                edgeArray[Chunk.size.width*Chunk.size.height + j*Chunk.size.width + i] = chunkX.blocks[i, j, 0]; // z*y(xEdge)
                edgeArray[j*Chunk.size.width + i] = chunkZ.blocks[0, j, i]; // x*y(zEdge)
                edgeArray[(Chunk.size.width + Chunk.size.width)*Chunk.size.height + i] = chunkC.blocks[0, i, 0]; // y
            }

        edgeBuffer.SetData(edgeArray);
    }

    void CreateBuffers () {
        int numPoints = Chunk.size.width * Chunk.size.height * Chunk.size.width;
        
        if (pointsBuffer == null || numPoints/4 != pointsBuffer.count) {            
            ReleaseBuffers ();
            
            int maxTriangleCount = (Chunk.size.width-1) * (Chunk.size.height-1) * (Chunk.size.width-1) * 5;
            triangleBuffer = new ComputeBuffer (maxTriangleCount, sizeof (float) * 3 * 3, ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer (numPoints/4, sizeof(byte)*4);
            triCountBuffer = new ComputeBuffer (1, sizeof (int), ComputeBufferType.Raw);
            edgeBuffer = new ComputeBuffer ((Chunk.size.width + Chunk.size.width + 1)*Chunk.size.height/4, sizeof(byte)*4);
        }
    }

    void ReleaseBuffers () {
        if (triangleBuffer != null) {
            triangleBuffer.Release();
            pointsBuffer.Release();
            triCountBuffer.Release();
            edgeBuffer.Release();
        }
    }

    void OnDestroy () {
        if (Application.isPlaying) {
            ReleaseBuffers ();
        }
    }
}

public struct Triangle {
#pragma warning disable 649 // disable unassigned variable warning
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;

    public Vector3 this [int i] {
        get {
            switch (i) {
                case 0:
                    return a;
                case 1:
                    return b;
                default:
                    return c;
            }
        }
    }
} 

public class MeshData {
	public Vector3[] vertices;
	public int[] triangles;
    public Material mat;
    public bool generateColliders;

	int triangleIndex;

	public MeshData(int numTris, Material mat, bool generateColliders) {
		vertices = new Vector3[(numTris) * 3];
		triangles = new int[(numTris) * 3];
        triangleIndex = 0;
        this.mat = mat;
        this.generateColliders = generateColliders;
	}

	public void AddTriangle(Triangle tri) {        
		vertices [triangleIndex] = tri[0];
		vertices [triangleIndex + 1] = tri[1];
		vertices [triangleIndex + 2] = tri[2];
        triangles [triangleIndex] = triangleIndex;
		triangles [triangleIndex + 1] = triangleIndex + 1;
		triangles [triangleIndex + 2] = triangleIndex + 2;
		triangleIndex += 3;
	}

	public Mesh CreateMesh() {
		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.RecalculateNormals ();
		return mesh;
	}
}