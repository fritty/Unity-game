using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

/* Handles mesh generation */
public class MeshGenerator : MonoBehaviour {
  
    [Header ("Mesh properties")]
    public Material mat;
    public bool generateColliders;

    public ComputeShader marchShader; // Shader for mesh generation

    public float chunksPerSecond = 2;


    // Buffers
    ComputeBuffer triangleBuffer; 
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer edgeBuffer;

    Queue<Vector3Int> requestedCoords = new Queue<Vector3Int>();
 

    // Set up from map
    Action<GeneratedDataInfo<MeshData>> meshCallback;
    Transform viewer;
    int viewDistance;

    Dictionary<Vector3Int,Chunk> existingChunks;


    void Start () {
        StartCoroutine("ManageRequests");
    }    

    IEnumerator ManageRequests() {
        Vector3Int coord = new Vector3Int();
        Vector3Int viewerCoord;
        bool start = true;
        // Endless coroutine loop
        while (true) {
            // Return requested data      
            if (!start && requestedCoords.Count > 0)
                meshCallback(new GeneratedDataInfo<MeshData>(CopyMeshData(), coord));
                    

            // Go through requested coordinates and start generation threads if still relevant
            if (requestedCoords.Count > 0) {
                viewerCoord = new Vector3Int(Mathf.RoundToInt(viewer.position.x / Chunk.size.width), 0, Mathf.RoundToInt(viewer.position.z / Chunk.size.width));
                coord = requestedCoords.Dequeue();
                
                // skip outdated coordinates
                while ((Mathf.Abs(coord.x - viewerCoord.x) > viewDistance || Mathf.Abs(coord.z - viewerCoord.z) > viewDistance) && requestedCoords.Count > 0) {
                    coord = requestedCoords.Dequeue();
                }
                
                if (Mathf.Abs(coord.x - viewerCoord.x) <= viewDistance && Mathf.Abs(coord.z - viewerCoord.z) <= viewDistance) {
                    Chunk chunk;
                    Chunk chunkX;
                    Chunk chunkZ;
                    Chunk chunkC;
                    if (existingChunks.TryGetValue(coord,out chunk) && existingChunks.TryGetValue(coord + Vector3Int.right,out chunkX) &&
                        existingChunks.TryGetValue(coord + new Vector3Int(0,0,1),out chunkZ) && existingChunks.TryGetValue(coord + Vector3Int.one - Vector3Int.up,out chunkC))
                    {
                       GenerateChunkMesh(chunk, chunkX, chunkZ, chunkC);                    
                       start = false;
                    }
                }   
            }

            // Wait for shader to finish
            yield return new WaitForSeconds(1f/chunksPerSecond);
        }
    }

    /* Interface */
    public void RequestMeshData (Vector3Int coord) {
		requestedCoords.Enqueue(coord);
	}

    public void SetMeshCallback (Action<GeneratedDataInfo<MeshData>> callback) {
        meshCallback = callback;
    }

    public void SetViewer (Transform viewer) {
        this.viewer = viewer;
    }

    public void SetViewDistance (int viewDistance) {
        this.viewDistance = viewDistance;
    }

    public void SetExistingChunks (Dictionary<Vector3Int,Chunk> existingChunks) {
        this.existingChunks = existingChunks;
    }


    /////////////////////
    /* Mesh generation */
    /////////////////////

    /* Generate chunk mesh based on its blocks */
    void GenerateChunkMesh (Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC) {  

        if (chunk == null || chunkX == null || chunkZ == null || chunkC == null) 
        {
            Debug.Log("null chunk reference");
            return;    
        }
        
        CreateBuffers ();
        
        // Current chunk mesh
        int kernelHandle = marchShader.FindKernel("March");
        
        pointsBuffer.SetData(chunk.blocks); // copy blocks data
        GenerateEdgeBuffer(chunkX, chunkZ, chunkC); // get edge points        
        DispatchMarchShader(kernelHandle, chunk.coord); // compute mesh
        //return new GeneratedDataInfo<MeshData>(CopyMeshData (chunk), chunk.coord);
    }  

    /* Add data from shader output to chunk mesh */
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
        marchShader.SetVector ("Origin", OriginFromCoord(coord));
        marchShader.SetBool ("ZEdgeGenerated", true);
        marchShader.SetBool ("XEdgeGenerated", true);
        marchShader.SetBool ("CornerEdgeGenerated", true);
            
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
   

    void OnDestroy () {
        if (Application.isPlaying) {
            ReleaseBuffers ();
        }
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