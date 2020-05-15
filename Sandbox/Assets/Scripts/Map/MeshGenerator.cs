using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MeshGenerator : MonoBehaviour {

    const int threadGroupSize = 8;

    [Header ("General Settings")]

    public MapGenerator mapGenerator;

    //public bool fixedMapSize;

    public Vector3Int numChunks = Vector3Int.one;

    public Transform viewer;
 
    public int viewDistance = 10; 

    [Space ()]
    public bool autoUpdateInEditor = true;
    public bool autoUpdateInGame = true;
    public ComputeShader marchShader;
    public Material mat;
    public bool generateColliders;

    [Header ("Voxel Settings")]
    public float isoLevel;
     
    public Vector3 offset = Vector3.zero;

    [Range (2, 100)]
    public int numPointsPerAxis = 30;

    [Header ("Gizmos")]
    public bool showBoundsGizmo = true;
    public Color boundsGizmoCol = Color.white;

    GameObject chunkHolder;
    const string chunkHolderName = "Chunks Holder";
    List<Chunk> chunks;
    Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> recycleableChunks;

    // Buffers
    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;

    ComputeBuffer edgeBuffer;
    bool[] toGenerate = {false,false,false};

    bool settingsUpdated;

    void Awake () {
        if (Application.isPlaying /*&& !fixedMapSize*/) {
            InitVariableChunkStructures ();
 
            var oldChunks = FindObjectsOfType<Chunk> ();
            for (int i = oldChunks.Length - 1; i >= 0; i--) {
                Destroy (oldChunks[i].gameObject);
            }
        }
    }

    void Update () {
        // Update endless terrain
        if ((Application.isPlaying /*&& !fixedMapSize*/)) {
            Run ();
        }

        if (settingsUpdated) {
            RequestMeshUpdate ();
            settingsUpdated = false;
        }
    }

    public void Run () {
        CreateBuffers ();

        if (Application.isPlaying) {
            InitVisibleChunks ();
        }

        // Release buffers immediately in editor
        if (!Application.isPlaying) {
            ReleaseBuffers ();
        }

    }

    public void RequestMeshUpdate () {
        if ((Application.isPlaying && autoUpdateInGame) || (!Application.isPlaying && autoUpdateInEditor)) {
            Run ();
        }
    }

    void InitVariableChunkStructures () {
        recycleableChunks = new Queue<Chunk> ();
        chunks = new List<Chunk> ();
        existingChunks = new Dictionary<Vector3Int, Chunk> ();
    }

    void InitVisibleChunks () {
        if (chunks==null) {
            return;
        }
        CreateChunkHolder ();
       
        Vector3 ps = viewer.position / Chunk.size.x;
        Vector3Int viewerCoord = new Vector3Int (Mathf.RoundToInt (ps.x), Mathf.RoundToInt (ps.y), Mathf.RoundToInt (ps.z));

        // Go through all existing chunks and flag for recyling if outside of max view dst
        for (int i = chunks.Count - 1; i >= 0; i--) {
            Chunk chunk = chunks[i];
            float chunkDistance = (chunk.coord - viewerCoord).magnitude;
            
            if (chunkDistance > viewDistance) {
                existingChunks.Remove (chunk.coord);
                recycleableChunks.Enqueue (chunk);
                chunk.mesh.Clear();
                chunks.RemoveAt (i); 
            }
        }

        for (int x = viewDistance; x >= -viewDistance; x--) {
            //for (int y = -viewDistance; y <= viewDistance; y++) {
                for (int z = viewDistance; z >= -viewDistance; z--) {
                    Vector3Int coord = new Vector3Int (x, 0, z) + viewerCoord;

                    if (existingChunks.ContainsKey (coord)) {
                        continue;
                    }

                    // Chunk is within view distance and should be created (if it doesn't already exist)
                    if (x*x + z*z <= viewDistance*viewDistance) {

                        Bounds bounds = new Bounds (OriginFromCoord (coord) + Vector3.one*(Chunk.size.x)/2f, Vector3.one * Chunk.size.x);
                        if (IsVisibleFrom (bounds, Camera.main)) {
                            if (recycleableChunks.Count > 0) {
                                Chunk chunk = recycleableChunks.Dequeue ();
                                chunk.coord = coord;
                                chunk.name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";
                                chunk.generated[0] = chunk.generated[1] = chunk.generated[2] = false;
                                chunk.mesh.Clear ();
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);
                                UpdateChunkMesh (chunk);
                            } else {
                                Chunk chunk = CreateChunk (coord);
                                chunk.coord = coord;
                                chunk.SetUp (mat, generateColliders);
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);
                                UpdateChunkMesh (chunk);
                            }
                        }
                    }

                }
           // }
        }
    }

    public bool IsVisibleFrom (Bounds bounds, Camera camera) {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes (camera);
        return GeometryUtility.TestPlanesAABB (planes, bounds);
    }

    public void UpdateChunkMesh (Chunk chunk) {
        
        // Current chunk mesh kernel
        int kernelHandle = marchShader.FindKernel("March");

        mapGenerator.Generate(pointsBuffer); // generate chunk points
        pointsBuffer.GetData(chunk.blocks);  // copy them to blocks data
        GenerateEdgeBuffer(chunk); // generate edge points

        DispatchMarchShader(kernelHandle, chunk.coord, false); // compute current mesh

        CopyMeshData (chunk); // copy mesh data to chunk

        // Edge mesh for adjacent chunks
        kernelHandle = marchShader.FindKernel("MarchEdge");
        Chunk edgeChunk;
        Vector3Int xEdge = new Vector3Int(1,0,0);
        Vector3Int zEdge = new Vector3Int(0,0,1);
        if (existingChunks.TryGetValue(chunk.coord - zEdge, out edgeChunk))
        {
            if (!edgeChunk.generated[0])
            {
                GenerateEdgeBuffer(edgeChunk);
                DispatchMarchShader(kernelHandle, edgeChunk.coord, true);
                CopyMeshData (edgeChunk);
            }
        }
        if (existingChunks.TryGetValue(chunk.coord - xEdge, out edgeChunk))
        {
            if (!edgeChunk.generated[1])
            {
                GenerateEdgeBuffer(edgeChunk);
                DispatchMarchShader(kernelHandle, edgeChunk.coord, true);
                CopyMeshData (edgeChunk);
            }
        }
        if (existingChunks.TryGetValue(chunk.coord - xEdge - zEdge, out edgeChunk))
        {
            if (!edgeChunk.generated[2])
            {
                GenerateEdgeBuffer(edgeChunk);
                DispatchMarchShader(kernelHandle, edgeChunk.coord, true);
                CopyMeshData (edgeChunk);
            }
        }
    }

    void CopyMeshData (Chunk chunk)
    {  
        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount (triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData (triCountArray);
        int numTris = triCountArray[0];

        if (numTris > 0)
        {
            Mesh mesh = new Mesh();

            // Get triangle data from shader
            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData (tris, 0, 0, numTris);

            var meshVertices = new Vector3[(numTris) * 3];
            var meshTriangles = new int[(numTris) * 3];

            CombineInstance[] combine = new CombineInstance[2];

            // add new mesh data
            for (int i = 0; i < numTris; i++) {
                for (int j = 0; j < 3; j++) {
                    meshTriangles[i * 3 + j] = i * 3 + j;
                    meshVertices[i * 3 + j] = tris[i][j];
                }
            } 

            mesh.vertices = meshVertices;
            mesh.triangles = meshTriangles;

            combine[0].mesh = Mesh.Instantiate(chunk.mesh);
            combine[1].mesh = mesh;   

            chunk.mesh.CombineMeshes(combine, true, false, false);
            chunk.mesh.RecalculateNormals ();

            chunk.generated[0] |= toGenerate[0];
            chunk.generated[1] |= toGenerate[1];
            chunk.generated[2] |= toGenerate[2];
            toGenerate[0] = toGenerate[1] = toGenerate[2] = false; // reset edge flag
        }
    }

    void DispatchMarchShader (int kernelHandle, Vector3Int coord, bool edge)
    {
        int threadGroupsX;
        int threadGroupsY;
        int threadGroupsZ;        

        if (edge)
        {
            if (!toGenerate[0] && !toGenerate[1] && !toGenerate[2]) return;

            threadGroupsX = Mathf.CeilToInt ((Chunk.size.x + Chunk.size.z - 1) / (float) threadGroupSize);
            threadGroupsY = Mathf.CeilToInt ((Chunk.size.y) / (float) threadGroupSize);
            threadGroupsZ = 1;
        }            
        else
        {
            threadGroupsX = threadGroupsZ = Mathf.CeilToInt ((Chunk.size.x) / (float) threadGroupSize);        
            threadGroupsY = Mathf.CeilToInt ((Chunk.size.y) / (float) threadGroupSize);        
        }        

        triangleBuffer.SetCounterValue (0);
        marchShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        marchShader.SetBuffer (kernelHandle, "edge", edgeBuffer);
        marchShader.SetBuffer (kernelHandle, "triangles", triangleBuffer);
        marchShader.SetInt ("Width", Chunk.size.x);
        marchShader.SetInt ("Height", Chunk.size.y);
        marchShader.SetVector ("Origin", OriginFromCoord(coord));
        marchShader.SetBool ("ZEdgeGenerated", toGenerate[0]);
        marchShader.SetBool ("XEdgeGenerated", toGenerate[1]);
        marchShader.SetBool ("CornerEdgeGenerated", toGenerate[2]);
        //toGenerate[0] = toGenerate[1] = toGenerate[2] = false; // reset edge flag
            
        marchShader.Dispatch (kernelHandle, threadGroupsX, threadGroupsY, threadGroupsZ);
    }

    void GenerateEdgeBuffer (Chunk chunk)
    {
        Vector3Int xEdge = new Vector3Int(1,0,0);
        Vector3Int zEdge = new Vector3Int(0,0,1);
        Chunk edgeChunk;
        byte[] edgeArray = new byte[(Chunk.size.x + Chunk.size.z + 1) * Chunk.size.y]; // x*y(zEdge) + z*y(xEdge) + y(Corner)

        if (!chunk.generated[0])
            if (existingChunks.TryGetValue(chunk.coord + zEdge, out edgeChunk))
            {
                for (int j = 0; j < Chunk.size.y; j++)
                    for (int i = 0; i < Chunk.size.x; i++)
                    {
                        edgeArray[j*Chunk.size.x + i] = edgeChunk.blocks[0, j, i]; // x*y(zEdge)
                    }
                toGenerate[0] = true;
            }
        if (!chunk.generated[1])
            if (existingChunks.TryGetValue(chunk.coord + xEdge, out edgeChunk))
            {
                for (int j = 0; j < Chunk.size.y; j++)
                    for (int i = 0; i < Chunk.size.z; i++)
                    {
                        edgeArray[Chunk.size.x*Chunk.size.y + j*Chunk.size.z + i] = edgeChunk.blocks[i, j, 0]; // z*y(xEdge)
                    }
                toGenerate[1] = true;    
            }        
        if (!chunk.generated[2])
            if (existingChunks.TryGetValue(chunk.coord + xEdge + zEdge, out edgeChunk) && toGenerate[0] && toGenerate[1])//&& existingChunks.ContainsKey(chunk.coord + xEdge) && existingChunks.ContainsKey(chunk.coord + zEdge))
            {
                for (int i = 0; i < Chunk.size.y; i++)
                {
                    edgeArray[(Chunk.size.x + Chunk.size.z)*Chunk.size.y + i] = edgeChunk.blocks[0, i, 0]; // y
                }
                toGenerate[2] = true; 
            }

        if (toGenerate[0] || toGenerate[1] || toGenerate[2]) edgeBuffer.SetData(edgeArray);
    }


    public void UpdateAllChunks () {

        // Create mesh for each chunk
        foreach (Chunk chunk in chunks) {
            UpdateChunkMesh (chunk);
        }

    }

    void OnDestroy () {
        if (Application.isPlaying) {
            ReleaseBuffers ();
        }
    }

    void CreateBuffers () {
        int numPoints = Chunk.size.x * Chunk.size.y * Chunk.size.z;

        // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
        // Otherwise, only create if null or if size has changed
        if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
            if (Application.isPlaying) {
                ReleaseBuffers ();
            }
            int maxTriangleCount = (Chunk.size.x-1) * (Chunk.size.y-1) * (Chunk.size.z-1) * 5;
            triangleBuffer = new ComputeBuffer (maxTriangleCount, sizeof (float) * 3 * 3, ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer (numPoints/4, sizeof(byte)*4);
            triCountBuffer = new ComputeBuffer (1, sizeof (int), ComputeBufferType.Raw);
            edgeBuffer = new ComputeBuffer ((Chunk.size.x + Chunk.size.z + 1)*Chunk.size.y/4, sizeof(byte)*4);
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

        return new Vector3 (coord.x, coord.y, coord.z) * Chunk.size.x;
    }

    void CreateChunkHolder () {
        // Create/find mesh holder object for organizing chunks under in the hierarchy
        if (chunkHolder == null) {
            if (GameObject.Find (chunkHolderName)) {
                chunkHolder = GameObject.Find (chunkHolderName);
            } else {
                chunkHolder = new GameObject (chunkHolderName);
            }
        }
    }

    Chunk CreateChunk (Vector3Int coord) {
        GameObject chunk = new GameObject ($"Chunk ({coord.x}, {coord.y}, {coord.z})");
        chunk.transform.parent = chunkHolder.transform;
        Chunk newChunk = chunk.AddComponent<Chunk> ();
        newChunk.coord = coord;
        return newChunk;
    }

    void OnValidate() {
        settingsUpdated = true;
    }

    struct Triangle {
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

    void OnDrawGizmos () {
        if (showBoundsGizmo) {
            Gizmos.color = boundsGizmoCol;

            List<Chunk> chunks = (this.chunks == null) ? new List<Chunk> (FindObjectsOfType<Chunk> ()) : this.chunks;
            foreach (var chunk in chunks) {
                Bounds bounds = new Bounds (OriginFromCoord (chunk.coord) + Vector3.one*(Chunk.size.x)/2f, Vector3.one * Chunk.size.x);
                Gizmos.color = boundsGizmoCol;
                Gizmos.DrawWireCube (OriginFromCoord (chunk.coord) + Vector3.one*(Chunk.size.x)/2f, Vector3.one * Chunk.size.x);
            }
        }
    }

}