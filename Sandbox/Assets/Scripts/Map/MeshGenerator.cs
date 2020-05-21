using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

/* Handles mesh generation */
public class MeshGenerator : MonoBehaviour {

    const int threadGroupSize = 8;  
  
    [Header ("Mesh properties")]
    public Material mat;
    public bool generateColliders;

    public ComputeShader marchShader; // Shader for mesh generation

    static Map map; // Reference for blocks data

    // Buffers
    ComputeBuffer triangleBuffer; 
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer edgeBuffer;

    bool[] toGenerate = {false,false,false}; // flags for generating edges

    Queue<ThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<ThreadInfo<MeshData>>();


    void Awake () {
        if (Application.isPlaying) {
            map = FindObjectOfType<Map>();
        }
    }    


    ////////////////////
    /* Multithreading */
    ////////////////////

    void Update() {
        // Return requested data in a main thread
        if (meshDataThreadInfoQueue.Count > 0) {
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
				ThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}    
    }

    /* Interface for requesting generation */
    public void RequestMeshData(Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC, Action<MeshData> callback) {
		ThreadStart threadStart = delegate {
			MeshDataThread (chunk, chunkX, chunkZ, chunkC, callback);
		};

        chunk.SetUpMesh(mat, generateColliders);

		new Thread (threadStart).Start ();
	}

	void MeshDataThread(Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC, Action<MeshData> callback) {
		MeshData meshData = GenerateChunkMesh(chunk, chunkX, chunkZ, chunkC);
		lock (meshDataThreadInfoQueue) {
			meshDataThreadInfoQueue.Enqueue (new ThreadInfo<MeshData> (callback, meshData));
		}
	}  


    /////////////////////
    /* Mesh generation */
    /////////////////////

    /* Generate chunk mesh based on its blocks */
    public MeshData GenerateChunkMesh (Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC) {        
        
        CreateBuffers ();
        
        // Current chunk mesh
        int kernelHandle = marchShader.FindKernel("March");
        
        pointsBuffer.SetData(chunk.blocks); // copy blocks data
        GenerateEdgeBuffer(chunk, chunkX, chunkZ, chunkC); // try to get edge points        
        DispatchMarchShader(kernelHandle, chunk.coord); // compute mesh
        return CopyMeshData (chunk); 

        // // Edge mesh for adjacent chunks
        // kernelHandle = marchShader.FindKernel("MarchEdge");
        // Chunk edgeChunk;
        // Vector3Int xEdge = new Vector3Int(1,0,0);
        // Vector3Int zEdge = new Vector3Int(0,0,1);
        // if (mapGenerator.existingChunks.TryGetValue(chunk.coord - zEdge, out edgeChunk))
        // {
        //     if (!edgeChunk.generated[0])
        //     {
        //         CreateMesh(edgeChunk, kernelHandle, true);
        //     }
        // }
        // if (mapGenerator.existingChunks.TryGetValue(chunk.coord - xEdge, out edgeChunk))
        // {
        //     if (!edgeChunk.generated[1])
        //     {
        //         CreateMesh(edgeChunk, kernelHandle, true);
        //     }
        // }
        // if (mapGenerator.existingChunks.TryGetValue(chunk.coord - xEdge - zEdge, out edgeChunk))
        // {
        //     if (!edgeChunk.generated[2])
        //     {                
        //         CreateMesh(edgeChunk, kernelHandle, true);
        //     }
        // }
    }  

    /* Add data from shader output to chunk mesh */
    MeshData CopyMeshData (Chunk chunk)
    {  
        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount (triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData (triCountArray);
        int numTris = triCountArray[0];

        if (numTris > 0)
        {
            MeshData meshData = new MeshData(numTris);
            // Get triangle data from shader

            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData (tris, 0, 0, numTris);

            //var meshVertices = new Vector3[(numTris) * 3];
            //var meshTriangles = new int[(numTris) * 3];            

            // add new mesh data
            for (int i = 0; i < numTris; i++) {
                meshData.AddTriangle(tris[i]);
                // for (int j = 0; j < 3; j++) {
                //     meshTriangles[i * 3 + j] = i * 3 + j;
                //     meshVertices[i * 3 + j] = tris[i][j];
                // }
            }            

            return meshData;
        }
        return null;
    }

    /* Execute mesh generation on GPU */
    void DispatchMarchShader (int kernelHandle, Vector3Int coord)
    {
        int threadGroupsX;
        int threadGroupsY;
        int threadGroupsZ;        

        triangleBuffer.SetCounterValue (0);
       
        threadGroupsX = threadGroupsZ = Mathf.CeilToInt ((Chunk.size.width) / (float) threadGroupSize);        
        threadGroupsY = Mathf.CeilToInt ((Chunk.size.height) / (float) threadGroupSize);  

        marchShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        marchShader.SetBuffer (kernelHandle, "edge", edgeBuffer);
        marchShader.SetBuffer (kernelHandle, "triangles", triangleBuffer);
        marchShader.SetInt ("Width", Chunk.size.width);
        marchShader.SetInt ("Height", Chunk.size.height);
        marchShader.SetVector ("Origin", OriginFromCoord(coord));
        marchShader.SetBool ("ZEdgeGenerated", toGenerate[0]);
        marchShader.SetBool ("XEdgeGenerated", toGenerate[1]);
        marchShader.SetBool ("CornerEdgeGenerated", toGenerate[2]);
            
        marchShader.Dispatch (kernelHandle, threadGroupsX, threadGroupsY, threadGroupsZ);
    }

    /* Copies edge data from adjacent chunks */
    void GenerateEdgeBuffer (Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC)
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
   

    void OnDestroy () {
        if (Application.isPlaying) {
            ReleaseBuffers ();
        }
    }

    void CreateBuffers () {
        int numPoints = Chunk.size.width * Chunk.size.height * Chunk.size.width;

        // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
        // Otherwise, only create if null or if size has changed
        if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
            if (Application.isPlaying) {
                ReleaseBuffers ();
            }
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

    Vector3 OriginFromCoord (Vector3Int coord) {
        return new Vector3 (coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
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

	public MeshData(int numTris) {
		vertices = new Vector3[(numTris) * 3];
		triangles = new int[(numTris) * 3];
        triangleIndex = 0;
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