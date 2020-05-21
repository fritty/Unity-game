using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

/* Manages chunks */
public class Map : MonoBehaviour {

    [Header ("General Settings")]

    public int viewDistance = 10;
    public bool cullView = false; // for destroying meshes outside viewDistance

    public Transform viewer;

    [Header ("Generators")]
    public MapGenerator mapGenerator;
    public MeshGenerator meshGenerator;

    [Header ("Gizmos")]
    public bool showBoundsGizmo = true;
    public Color boundsGizmoCol = Color.white;
    public bool showCellGizmo = false;
    public Color boundsCellCol = Color.white;
    
    
    [HideInInspector]
    public Dictionary<Vector3Int, Chunk> existingChunks;
    Dictionary<Vector3Int, Chunk> chunksToGenerate;

    GameObject chunkHolder;
    const string chunkHolderName = "Chunks Holder";

    Vector3Int viewerCoord = new Vector3Int();

    Vector3Int generationGizmo = new Vector3Int();


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

    void Start () {
        Chunk chunk;
        Vector3Int coord;
        for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                coord = new Vector3Int(x, 0, z);
                chunk = CreateChunk();
                chunk.SetCoord(new Vector3Int(x, 0, z));
                existingChunks.Add(coord, chunk);
            }
    }

    void OnDrawGizmos () {
        if (Application.isPlaying) {
            var chunks = existingChunks.Values;            
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
            if (showBoundsGizmo) {
                chunks = chunksToGenerate.Values;
                Gizmos.color = Color.red;
                foreach (var chunk in chunks) {
                    Gizmos.DrawWireCube (OriginFromCoord (chunk.coord) + Vector3.one*(Chunk.size.width)/2f, Vector3.one * Chunk.size.width);
                }
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube (OriginFromCoord (generationGizmo) + Vector3.one*(Chunk.size.width)/2f, Vector3.one * Chunk.size.width);
            }
        }
    }


    ///////////////////////
    /* Chunks management */
    ///////////////////////
    
    /* Create/destroy chunks based on view distance */
    void InitVisibleChunks () {
        if (existingChunks==null) {
            return;
        }
               
        Vector3Int currentCoord = new Vector3Int();        
        currentCoord.x = Mathf.RoundToInt(viewer.position.x / Chunk.size.width);
        currentCoord.y = 0;
        currentCoord.z = Mathf.RoundToInt(viewer.position.z / Chunk.size.width);
        
        // Go through world bound difference and delete/mark for generation
        if (viewerCoord != currentCoord){ // only if coord is changed
            Vector3Int previousCoord = viewerCoord;

            Vector3Int[] regionX = new Vector3Int[2];
            Vector3Int[] regionZ = new Vector3Int[2];

            Chunk tmpChunk;

            // regions for recalculation
            regionX[0] = new Vector3Int(currentCoord.x > previousCoord.x ? previousCoord.x - viewDistance 
                                                                         : Mathf.Max(currentCoord.x + viewDistance + 1, previousCoord.x - viewDistance), 0, 
                                        previousCoord.z - viewDistance);
            regionX[1] = new Vector3Int(currentCoord.x > previousCoord.x ? Mathf.Min(currentCoord.x - viewDistance, previousCoord.x + viewDistance + 1) 
                                                                         : previousCoord.x + viewDistance + 1, 0, 
                                        previousCoord.z + viewDistance + 1);

            if (Mathf.Abs(currentCoord.z - previousCoord.z) < 2*viewDistance + 1)
            {
                regionZ[0] = new Vector3Int(Mathf.Max(currentCoord.x - viewDistance, previousCoord.x - viewDistance), 0, 
                                        currentCoord.z > previousCoord.z ? previousCoord.z - viewDistance : currentCoord.z + viewDistance + 1);
                regionZ[1] = new Vector3Int(Mathf.Min(currentCoord.x + viewDistance + 1, previousCoord.x + viewDistance + 1), 0, 
                                        currentCoord.z > previousCoord.z ? currentCoord.z - viewDistance : previousCoord.z + viewDistance + 1);
            }
            else
            {
                regionZ[0] = regionZ[1] = new Vector3Int(0, 0, 0);
            }

            
            // loop through old regions and mark them for regeneration as new
            for (int x = regionX[0].x; x < regionX[1].x; x++) {
                for (int z = regionX[0].z; z < regionX[1].z; z++)
                {
                    Vector3Int key = new Vector3Int(x, 0, z);
                    if (existingChunks.TryGetValue(key, out tmpChunk)) {
                        existingChunks.Remove(key);
                    }
                    else if (chunksToGenerate.TryGetValue(key, out tmpChunk)) {
                        chunksToGenerate.Remove(key);
                    }
                    else
                    {
                        Debug.Log("no generated chunk found");
                    }

                    if (Mathf.Abs(currentCoord.x - previousCoord.x) < 2*viewDistance + 1)
                    {
                        if (currentCoord.x > previousCoord.x)
                            key = key + new Vector3Int(2*viewDistance + 1, 0, currentCoord.z - previousCoord.z);
                        else
                            key = key + new Vector3Int(-2*viewDistance - 1, 0, currentCoord.z - previousCoord.z);
                    }
                    else
                    {
                        key = new Vector3Int(x + currentCoord.x - previousCoord.x, 0, z + currentCoord.z - previousCoord.z);
                    }

                    mapGenerator.RequestMapData(key, OnMapDataReceived);                                                
                    chunksToGenerate.Add(key, tmpChunk);
                    tmpChunk.SetCoord(key);
                }
            }
            for (int z = regionZ[0].z; z < regionZ[1].z; z++) {
                for (int x = regionZ[0].x; x < regionZ[1].x; x++)
                {
                    Vector3Int key = new Vector3Int(x, 0, z);
                    if (existingChunks.TryGetValue(key, out tmpChunk)) {
                        existingChunks.Remove(key);
                    }
                    else if (chunksToGenerate.TryGetValue(key, out tmpChunk)) {
                        chunksToGenerate.Remove(key);
                    }
                    else
                    {
                        Debug.Log("no generated chunk found");
                    }

                    if (currentCoord.z > previousCoord.z)
                        key = key + new Vector3Int(0, 0, 2*viewDistance+1); 
                    else  
                        key = key + new Vector3Int(0, 0, -2*viewDistance-1); 

                    mapGenerator.RequestMapData(key, OnMapDataReceived);                                                
                    chunksToGenerate.Add(key, tmpChunk);
                    tmpChunk.SetCoord(key);
                }
            }            

            viewerCoord = currentCoord;
        }

        // Go through view distance and create missing meshes
               
    }

    void OnMapDataReceived(MapData mapData) {
        Chunk chunk;
        if (chunksToGenerate.TryGetValue(mapData.coord, out chunk))
        {
            chunksToGenerate.Remove(mapData.coord);
            chunk.blocks = mapData.blocks;
            chunk.SetCoord(mapData.coord);
            existingChunks.Add(mapData.coord, chunk);

            //meshGenerator.RequestMeshData(chunk,)
        }

        generationGizmo = mapData.coord;
    }

    // void OnMeshDataReceived(MeshData meshData) {
    //     meshFilter.mesh = meshData.CreateMesh ();
    // }

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

    Chunk CreateChunk () {
        GameObject chunk = new GameObject();
        chunk.transform.parent = chunkHolder.transform;
        Chunk newChunk = chunk.AddComponent<Chunk> ();
        //newChunk.SetCoord(coord);
        return newChunk;
    }

    void InitVariableChunkStructures () {        
        existingChunks = new Dictionary<Vector3Int, Chunk> ();
        chunksToGenerate = new Dictionary<Vector3Int, Chunk> ();
    }    
}

// structure for managing generation threads
public struct ThreadInfo<T> {
		public readonly Action<T> callback;
		public readonly T parameter;

		public ThreadInfo (Action<T> callback, T parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}
		
	}