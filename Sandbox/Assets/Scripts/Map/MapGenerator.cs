using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Handles generation of chunks */
public class MapGenerator : MonoBehaviour {

    [Header ("General Settings")]

    public int viewDistance = 10;
    public bool cullView = false; // for destroying meshes outside viewDistance

    public Transform viewer;

    [Header ("Genrators Settings")]
    public float noiseScale = Chunk.size.height;
    public float noiseFrequency = 0.025f;
    public ComputeShader mapShader;

    [Header ("Gizmos")]
    public bool showBoundsGizmo = true;
    public Color boundsGizmoCol = Color.white;
    public bool showCellGizmo = false;
    public Color boundsCellCol = Color.white;

    
    [HideInInspector]
    public List<Chunk> chunks;
    public Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> recycleableChunks;

    GameObject chunkHolder;
    const string chunkHolderName = "Chunks Holder";

    Vector3Int viewerCoord = new Vector3Int();


    void Update () {
        // Update endless terrain
        if ((Application.isPlaying)) {
            InitVisibleChunks ();
        }
    }
    
    void Awake () {
        if (Application.isPlaying) {
            InitVariableChunkStructures ();
            CreateChunkHolder ();
 
            var oldChunks = FindObjectsOfType<Chunk> ();
            for (int i = oldChunks.Length - 1; i >= 0; i--) {
                Destroy (oldChunks[i].gameObject);
            }
        }
    }

    void OnDrawGizmos () {
        if (Application.isPlaying) {
            foreach (var chunk in chunks) {                
                if (showBoundsGizmo) {
                    Gizmos.color = boundsGizmoCol;
                    Gizmos.DrawWireCube (OriginFromCoord (chunk.coord) + Vector3.one*(Chunk.size.width)/2f, Vector3.one * Chunk.size.width);
                }
                if (showCellGizmo) {
                    Gizmos.color = boundsCellCol;
                    Vector3 center = OriginFromCoord (chunk.coord);
                    Vector3 size = new Vector3(0.1f, 0.1f, 0.1f);
                    for (int x = 0; x < Chunk.size.width; x++)
                        for (int z = 0; z < Chunk.size.width; z++)
                            for (int y = 0; y < Chunk.size.height; y++)
                                {
                                    if (chunk.blocks[z,y,x] < 255 && chunk.blocks[z,y,x] > 0) {
                                    Vector3 offset = new Vector3(x, y, z);
                                    Gizmos.color = new Color(boundsCellCol.r, boundsCellCol.g, boundsCellCol.b, chunk.blocks[z,y,x] / 255f);
                                    Gizmos.DrawWireCube(center + offset, size);
                                    }
                                }
                }
            }
        }
    }


    ////////////////////
    /* Map generators */
    ////////////////////

    public byte[,,] Generate (Vector3 origin) {
        //return FlatGen(origin);
        //return GradGen(origin);
        return PerlinGen(origin);
    }

    byte[,,] FlatGen (Vector3 origin){
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
        Vector3 tilt = new Vector3((origin.x/32 + 120)/256,0,0);
        float val;

        for (byte x = 0; x < Chunk.size.width; x++){
            for (byte z = 0; z < Chunk.size.width; z++){     
                val = tilt.x*x + tilt.z*z;           
                for (byte y = 0; y < Chunk.size.height; y++){                    
                    blocks[z,y,x] = HeightToByte(y, val);
                }
            }
        }
        return blocks;
    }

    byte[,,] GradGen (ComputeBuffer pointsBuffer, Vector3 origin) {
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
        float xAbs;
        //float zAbs;
        float val;

        for (byte x = 0; x < Chunk.size.width; x++){
            for (byte z = 0; z < Chunk.size.width; z++){  
                xAbs = origin.x + x;
                //zAbs = origin.z + z;
                val = (noiseScale-2) * Mathf.Pow(Mathf.Sin(xAbs*noiseFrequency),2);              
                for (byte y = 0; y < Chunk.size.height; y++){                     
                    blocks[z,y,x] = HeightToByte(y, val);
                }
            }
        }
        return blocks;        
    }

    byte[,,] PerlinGen (Vector3 origin) {
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];        
        Vector2 noiseOffset = new Vector2(500, 500);
        float noise;

        for (byte x = 0; x < Chunk.size.width; x++){
            for (byte z = 0; z < Chunk.size.width; z++){
                noise = noiseScale * Mathf.PerlinNoise((x + origin.x) * noiseFrequency + noiseOffset.x, (z + origin.z) * noiseFrequency + noiseOffset.y);
                for (byte y = 0; y < Chunk.size.height; y++){
                    blocks[z,y,x] = HeightToByte(y, noise);
                }            
            }              
        }
        return blocks;
    }

    byte HeightToByte(byte y, float val) {
        if (y == Mathf.RoundToInt(val))
            if (y > val)
                return (byte)(Mathf.RoundToInt(255 *  (val - Mathf.Floor(val))) - 127);
            else
                return (byte)(Mathf.RoundToInt(255 *  (val + 1 - Mathf.Floor(val))) - 127);
        if (y > val)
            return 0;
        else
            return 255; 
    }

    byte[,,] ShaderGen (Vector3 origin){
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
        ComputeBuffer pointsBuffer = new ComputeBuffer(Chunk.size.width * Chunk.size.height * Chunk.size.width / 4, 4);
        int kernelHandle = mapShader.FindKernel("MapGen");
        int threadsPerAxis = 8; 
        
        mapShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        mapShader.SetInt ("Width", Chunk.size.width);
        mapShader.SetFloat ("Height", Chunk.size.height);  
        
        mapShader.Dispatch (kernelHandle, Chunk.size.width / (threadsPerAxis*4), Chunk.size.height / (threadsPerAxis), Chunk.size.width);

        pointsBuffer.GetData(blocks);
        pointsBuffer.Release();
        return blocks;
    }  


    ///////////////////////
    /* Chunks management */
    ///////////////////////
    
    /* Create/destroy chunks based on view distance */
    void InitVisibleChunks () {
        if (chunks==null) {
            return;
        }
               
        Vector3Int ps = new Vector3Int();        
        ps.x = Mathf.RoundToInt(viewer.position.x / Chunk.size.width);
        ps.y = 0;
        ps.z = Mathf.RoundToInt(viewer.position.z / Chunk.size.width);
        
        // Go through all existing chunks and flag for recyling if outside of max view dst
        if (viewerCoord != ps){ // only if coord is changed
            viewerCoord = ps;            
            for (int i = chunks.Count - 1; i >= 0; i--) {
                Chunk chunk = chunks[i];
                float chunkDistance = (chunk.coord - viewerCoord).magnitude;
                
                if (chunkDistance > viewDistance) {                
                    recycleableChunks.Enqueue (chunk);
                    if (cullView){
                        existingChunks.Remove (chunk.coord);
                        chunk.mesh.Clear();
                        chunks.RemoveAt (i); 
                    }                
                }
            }
        }

        // Go through view distance and create missing chunks
        for (int x = viewDistance; x >= -viewDistance; x--) {
            for (int z = viewDistance; z >= -viewDistance; z--) {
                Vector3Int coord = new Vector3Int (x + viewerCoord.x, 0, z + viewerCoord.z);

                if (existingChunks.ContainsKey (coord)) {
                    continue;
                }

                // Chunk is within view distance and should be created (if it doesn't already exist)
                if (x*x + z*z <= viewDistance*viewDistance) {

                    Bounds bounds = new Bounds (OriginFromCoord (coord) + Vector3.one*(Chunk.size.width)/2f, Vector3.one * Chunk.size.width);
                    if (IsVisibleFrom (bounds, Camera.main)) {
                        if (cullView) {
                            if (recycleableChunks.Count > 0) {
                                Chunk chunk = recycleableChunks.Dequeue ();                               
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);

                                chunk.SetUp(coord);
                                chunk.blocks = Generate(OriginFromCoord(chunk.coord));
                                chunk.UpdateMesh();
                            } else {
                                Chunk chunk = CreateChunk (coord);                               
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);

                                chunk.blocks = Generate(OriginFromCoord(chunk.coord));
                                chunk.UpdateMesh();
                            }
                        }
                        else {
                            bool isRecycled = false;
                            for (int i = 0; i < recycleableChunks.Count; i++) {
                                Chunk chunk = recycleableChunks.Dequeue ();
                                if ((chunk.coord - viewerCoord).magnitude > viewDistance) {
                                    existingChunks.Remove (chunk.coord);                                                                    
                                    existingChunks.Add (coord, chunk);

                                    chunk.SetUp(coord);
                                    chunk.blocks = Generate(OriginFromCoord(chunk.coord));
                                    chunk.UpdateMesh();
                                    isRecycled = true;
                                    break;
                                }
                            }
                            if (!isRecycled) {
                                Chunk chunk = CreateChunk (coord);                               
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);

                                chunk.blocks = Generate(OriginFromCoord(chunk.coord));
                                chunk.UpdateMesh();
                            }
                        }  
                    }
                }
            }                
        }        
    }

    bool IsVisibleFrom (Bounds bounds, Camera camera) {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes (camera);
        return GeometryUtility.TestPlanesAABB (planes, bounds);
    }    
    
    Vector3 OriginFromCoord (Vector3Int coord) {
        return new Vector3 (coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
    }

    /* Create/find chunk holder object for organizing chunks under in the hierarchy */
    void CreateChunkHolder () {        
        if (chunkHolder == null) {
            if (GameObject.Find (chunkHolderName)) {
                chunkHolder = GameObject.Find (chunkHolderName);
                chunkHolder.transform.SetParent(transform);
            } else {
                chunkHolder = new GameObject (chunkHolderName);
                chunkHolder.transform.SetParent(transform);
            }
        }
    }

    Chunk CreateChunk (Vector3Int coord) {
        GameObject chunk = new GameObject();
        chunk.transform.parent = chunkHolder.transform;
        Chunk newChunk = chunk.AddComponent<Chunk> ();
        newChunk.SetUp(coord);
        return newChunk;
    }

    void InitVariableChunkStructures () {
        recycleableChunks = new Queue<Chunk> ();
        chunks = new List<Chunk> ();
        existingChunks = new Dictionary<Vector3Int, Chunk> ();
    }
}