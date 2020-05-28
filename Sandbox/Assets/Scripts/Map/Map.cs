using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

/* Manages chunks */
public class Map : MonoBehaviour {

    [Header ("General Settings")]
    public int viewDistance = 10;
    public Transform viewer;

    [Header ("Generators")]
    public MapGenerator mapGenerator;
    public MeshGenerator meshGenerator;

    [Header ("Gizmos")]
    [SerializeField]
    bool showBoundsGizmo = true;
    [SerializeField]
    Color chunksGizmoCol = Color.white;
    [SerializeField]
    Color generatedChunksGizmoCol = Color.green;
    [SerializeField]
    bool showBlocksGizmo = false;
    
    
    public Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> chunksForRecycling;

    GameObject chunkHolder;
    const string chunkHolderName = "Chunks Holder";

    Vector3Int viewerCoord;

    Vector3Int generationGizmo;


    void Update () {
        // Update terrain
        RequestVisibleChunks ();
    }

    void FixedUpdate () {
        mapGenerator.ManageRequests();
    }

    void LateUpdate() { 
        meshGenerator.ManageRequests();
    }
    
    void Awake () { 
        InitVariableChunkStructures ();
        CreateChunkHolder ();

        var oldChunks = FindObjectsOfType<Chunk> ();
        for (int i = oldChunks.Length - 1; i >= 0; i--) {
            Destroy (oldChunks[i].gameObject);
        }
        
        generationGizmo = new Vector3Int(0, 0, 0);
        viewerCoord = new Vector3Int(0, 0, 0);
        
        mapGenerator.SetCallback(OnMapDataReceived);
        mapGenerator.SetMapReference(this);

        meshGenerator.SetCallback(OnMeshDataReceived);
        meshGenerator.SetMapReference(this);

        transform.position = Vector3.zero;
    }

    void Start () {
        Chunk chunk;
        Vector3Int coord;
        for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                coord = new Vector3Int(x, 0, z);
                chunk = CreateChunk();
                chunksForRecycling.Enqueue(chunk);
                mapGenerator.RequestData(coord);
            }
    }  


    ///////////////////////
    /* Chunks management */
    ///////////////////////
    
    /* Create/destroy chunks based on view distance */
    void RequestVisibleChunks () {
        if (existingChunks==null) {
            return;
        }
               
        Vector3Int currentCoord = new Vector3Int(Mathf.FloorToInt(viewer.position.x / Chunk.size.width), 0, Mathf.FloorToInt(viewer.position.z / Chunk.size.width));
        
        // Go through world bound difference and delete/mark for generation
        if (viewerCoord != currentCoord){ // only if coord is changed
            Vector3Int previousCoord = viewerCoord;

            Vector3Int[] regionX = new Vector3Int[2];
            Vector3Int[] regionZ = new Vector3Int[2];

            // regions for recalculation
            regionX[0] = new Vector3Int(currentCoord.x > previousCoord.x ? previousCoord.x - viewDistance 
                                                                         : Mathf.Max(currentCoord.x + viewDistance + 1, previousCoord.x - viewDistance), 0, 
                                        previousCoord.z - viewDistance);
            regionX[1] = new Vector3Int(currentCoord.x > previousCoord.x ? Mathf.Min(currentCoord.x - viewDistance, previousCoord.x + viewDistance + 1) 
                                                                         : previousCoord.x + viewDistance + 1, 0, 
                                        previousCoord.z + viewDistance + 1);

            
            regionZ[0] = new Vector3Int(Mathf.Max(currentCoord.x - viewDistance, previousCoord.x - viewDistance), 0, 
                                        currentCoord.z > previousCoord.z ? previousCoord.z - viewDistance 
                                                                         : Mathf.Max(currentCoord.z + viewDistance + 1,previousCoord.z - viewDistance));
            regionZ[1] = new Vector3Int(Mathf.Min(currentCoord.x + viewDistance + 1, previousCoord.x + viewDistance + 1), 0, 
                                        currentCoord.z > previousCoord.z ? Mathf.Min(currentCoord.z - viewDistance, previousCoord.z + viewDistance + 1) 
                                                                         : previousCoord.z + viewDistance + 1);
           

            
            // loop through old regions and mark them for regeneration as new
            for (int x = regionX[0].x; x < regionX[1].x; x++) {
                for (int z = regionX[0].z; z < regionX[1].z; z++)
                {
                    Chunk tmpChunk;
                    Vector3Int key = new Vector3Int(x, 0, z);
                    if (existingChunks.TryGetValue(key, out tmpChunk)) {
                        existingChunks.Remove(key);
                        chunksForRecycling.Enqueue(tmpChunk);
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

                    mapGenerator.RequestData(key); 
                }
            }
            for (int z = regionZ[0].z; z < regionZ[1].z; z++) {
                for (int x = regionZ[0].x; x < regionZ[1].x; x++)
                {
                    Chunk tmpChunk;
                    Vector3Int key = new Vector3Int(x, 0, z);
                    if (existingChunks.TryGetValue(key, out tmpChunk)) {
                        existingChunks.Remove(key);
                        chunksForRecycling.Enqueue(tmpChunk);
                    }    

                    if (Mathf.Abs(currentCoord.z - previousCoord.z) < 2*viewDistance + 1)
                    {
                        if (currentCoord.z > previousCoord.z)
                            key = key + new Vector3Int(0, 0, 2*viewDistance+1); 
                        else  
                            key = key + new Vector3Int(0, 0, -2*viewDistance-1); 
                    }
                    else
                    {
                        key = new Vector3Int(x, 0, z + currentCoord.z - previousCoord.z);
                    }

                    mapGenerator.RequestData(key);
                }
            }            

            viewerCoord = currentCoord;
        }      
    }

    void OnMapDataReceived(GeneratedDataInfo<MapData> mapData) {
        if (Mathf.Abs(mapData.coord.x - viewerCoord.x) <= viewDistance && 
            Mathf.Abs(mapData.coord.z - viewerCoord.z) <= viewDistance && 
            !existingChunks.ContainsKey(mapData.coord))
        {
            Chunk chunk = chunksForRecycling.Dequeue();
            chunk.SetBlocks(mapData.data.blocks);
            chunk.SetCoord(mapData.coord);
            existingChunks.Add(mapData.coord, chunk);

            TryToGenerateMesh(chunk);
            if (existingChunks.TryGetValue(mapData.coord - new Vector3Int(1, 0, 0), out chunk))
                TryToGenerateMesh(chunk);
            if (existingChunks.TryGetValue(mapData.coord - new Vector3Int(0, 0, 1), out chunk))
                TryToGenerateMesh(chunk);
            if (existingChunks.TryGetValue(mapData.coord - new Vector3Int(1, 0, 1), out chunk))
                TryToGenerateMesh(chunk);
        }
        generationGizmo = mapData.coord;
    }

    void TryToGenerateMesh (Chunk chunk) {
        if (chunk.HasMesh())
            return;
        Chunk chunkX;
        Chunk chunkZ;
        Chunk chunkC;
        if (existingChunks.TryGetValue(chunk.coord + new Vector3Int(1,0,0), out chunkX) &&
            existingChunks.TryGetValue(chunk.coord + new Vector3Int(0,0,1), out chunkZ) &&
            existingChunks.TryGetValue(chunk.coord + new Vector3Int(1,0,1), out chunkC))
        {
            meshGenerator.RequestData(chunk.coord);
        }    
    }

    void OnMeshDataReceived(GeneratedDataInfo<MeshData> meshData) {
        Chunk chunk;
        if (existingChunks.TryGetValue(meshData.coord, out chunk))
        {
            chunk.SetUpMesh(meshData.data);
        } 
        generationGizmo = meshData.coord;     
    }

    bool IsVisibleFrom (Bounds bounds, Camera camera) {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes (camera);
        return GeometryUtility.TestPlanesAABB (planes, bounds);
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
        //chunksToGenerate = new Dictionary<Vector3Int, Chunk> ();
        chunksForRecycling = new Queue<Chunk>();
    }    

    void OnDrawGizmos () {
        if (Application.isPlaying) {
            var chunks = existingChunks.Values;            
            foreach (var chunk in chunks) {
                // chunks            
                if (showBoundsGizmo) {
                    if (chunk.HasMesh())
                        Gizmos.color = generatedChunksGizmoCol;
                    else
                        Gizmos.color = chunksGizmoCol;
                    Gizmos.DrawWireCube (OriginFromCoord (chunk.coord) + Vector3.one*(Chunk.size.width)/2f, Vector3.one * Chunk.size.width);
                }

                // surface blocks for player chunk
                if (showBlocksGizmo && chunk.coord == viewerCoord) {
                    Vector3 center = OriginFromCoord (chunk.coord);
                    Vector3 size = Vector3.one * 1;
                    for (int x = 0; x < Chunk.size.width; x++)
                        for (int z = 0; z < Chunk.size.width; z++)
                            for (int y = 0; y < Chunk.size.height; y++)
                                {
                                    if (chunk.blocks[z,y,x] < 255 && chunk.blocks[z,y,x] > 0) {
                                    Vector3 offset = new Vector3(x, y, z);
                                    size = Vector3.up * (chunk.blocks[z, y, x]/255f);
                                    Gizmos.color = new Color(0, 1, 0, 1);                                    
                                    Gizmos.DrawLine(center+offset, center+offset + size);

                                    offset = new Vector3(x, y + chunk.blocks[z, y, x]/(255f), z);
                                    size = Vector3.up;
                                    Gizmos.color = new Color(1, 0, 0, 1);
                                    Gizmos.DrawLine(center+offset, center+offset + size);

                                    offset = new Vector3(x, y, z);
                                    size = Vector3.one * .1f;
                                    Gizmos.color = new Color(0, 0, 1, .5f);
                                    Gizmos.DrawWireCube(center + offset, size);
                                    
                                    }
                                }
                }
            }

            // world bounds
            if (showBoundsGizmo) {                
                Gizmos.color = Color.red; // currently generated chunk
                Gizmos.DrawWireCube (OriginFromCoord (generationGizmo) + Vector3.one*(Chunk.size.width)/2f, Vector3.one * Chunk.size.width);
                
                Gizmos.color = Color.green;
                Vector3 worldOrigin = OriginFromCoord(viewerCoord - new Vector3Int(viewDistance, -1, viewDistance));
                Vector3 worldSize = new Vector3(Chunk.size.width * (2*viewDistance+1), 0, Chunk.size.width * (2*viewDistance+1));
                Gizmos.DrawLine(worldOrigin, worldOrigin + worldSize - new Vector3(worldSize.x, 0, 0));
                Gizmos.DrawLine(worldOrigin + worldSize - new Vector3(worldSize.x, 0, 0), worldOrigin + worldSize);
                Gizmos.DrawLine(worldOrigin + worldSize, worldOrigin + worldSize - new Vector3(0, 0, worldSize.z));
                Gizmos.DrawLine(worldOrigin + worldSize - new Vector3(0, 0, worldSize.z), worldOrigin);
            }
        }
    }

    Vector3 OriginFromCoord (Vector3Int coord) {
        return new Vector3 (coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
    }
}

// structure for managing generation threads
public struct GeneratedDataInfo<T> {
        public readonly T data;
		public readonly Vector3Int coord;

		public GeneratedDataInfo (T data, Vector3Int coord)
		{
			this.coord = coord;
			this.data = data;
		}
		
	}