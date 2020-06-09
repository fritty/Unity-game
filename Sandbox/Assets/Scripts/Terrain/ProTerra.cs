using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Procedural Terrain. Creates and manages chunks */
[RequireComponent(typeof(DiggingListener))]
[DisallowMultipleComponent]
public class ProTerra : MonoBehaviour
{      
    public static ProTerra Instance;

    [Header("General Settings")]
    public int viewDistance = 10;
    public Transform viewer;

    //static int worldHeight = 1; // not implemented     

    [Header("Gizmos")]
    [SerializeField]                                              
    bool showBoundsGizmo = true;
    [SerializeField]
    Color chunksGizmoCol = Color.white;
    [SerializeField]
    Color generatedChunksGizmoCol = Color.green;

    [Header("Generator settings")]
    public BlocksGeneratorSettings blocksGeneratorSettings;
    public MeshGeneratorSettings meshGeneratorSettings;

    [HideInInspector]
    public bool blocksGeneratorSettingsFoldout;
    [HideInInspector]
    public bool meshGeneratorSettingsFoldout;

    // components
    BlocksGenerator blocksGenerator;
    IMeshGenerator meshGenerator;

    DiggingListener diggingListener; 
    //
    
    public Vector3Int viewerCoord { get; private set; }

    // structures for managing chunks 
    public Dictionary<Vector3Int, Chunk> existingChunks { get; private set; }
    Queue<Chunk> chunksForRecycling;
    //
                  

    void Awake()
    {
        Initialize();        
    }

    void Start()
    {
        CreateChunks();       
    }

    void FixedUpdate()
    {
        RequestVisibleChunks();
        blocksGenerator.ManageRequests();
    }

    void LateUpdate()
    {
        meshGenerator.ManageRequests();
    }


    /* Interface */

    // Tries to modyfy block. If successfull, requests mesh and returns true
    public bool ModifyBlock (Vector3Int chunkCoord, Vector3Int localPosition, int value)
    {
        Chunk chunk;
        if (existingChunks.TryGetValue(chunkCoord, out chunk))
        {
            if (chunk.ModifyBlock(localPosition, value))
            {
                MarkForMeshGeneration(chunk);

                for (int i = 1; i < 8; i++)
                {
                    int x = i & 1;
                    int y = (i & 2) >> 1;
                    int z = (i & 4) >> 2;

                    if ((localPosition.x * x == 0) && (localPosition.y * y == 0) && (localPosition.z * z == 0) && existingChunks.TryGetValue(chunkCoord - new Vector3Int(x, y, z), out chunk))
                        MarkForMeshGeneration(chunk);
                }
                return true;  
            }                 
        } 

        return false;
    }


   
    /* Static methods */

    public static Vector3Int WorldPositionToChunkCoord(Vector3Int position)
    {
        return new Vector3Int(Mathf.FloorToInt((float)position.x / Chunk.size.width), Mathf.FloorToInt((float)position.y / Chunk.size.height), Mathf.FloorToInt((float)position.z / Chunk.size.width));
    }

    public static Vector3Int WorldPositionToChunkCoord(Vector3 position)
    {
        return new Vector3Int(Mathf.FloorToInt(position.x / Chunk.size.width), Mathf.FloorToInt(position.y / Chunk.size.height), Mathf.FloorToInt(position.z / Chunk.size.width));
    }

    public static Vector3Int WorldPositionToChunkPosition(Vector3Int position)
    {
        Vector3Int result = new Vector3Int(position.x % Chunk.size.width, position.y % Chunk.size.height, position.z % Chunk.size.width);
        if (result.x < 0)
            result.x += Chunk.size.width;
        if (result.y < 0)
            result.y += Chunk.size.height;
        if (result.z < 0)
            result.z += Chunk.size.width;

        return result;
    }

    public static Vector3Int WorldPositionToChunkPosition(Vector3 position)
    {
        Vector3Int result = new Vector3Int(Mathf.FloorToInt(position.x) % Chunk.size.width, Mathf.FloorToInt(position.y) % Chunk.size.height, Mathf.FloorToInt(position.z) % Chunk.size.width);
        if (result.x < 0)
            result.x += Chunk.size.width;
        if (result.y < 0)
            result.y += Chunk.size.height;
        if (result.z < 0)
            result.z += Chunk.size.width;

        return result;
    }

    public static Vector3Int ChunkOriginFromCoord(Vector3Int chunkCoord)
    {
        return new Vector3Int(chunkCoord.x * Chunk.size.width, chunkCoord.y * Chunk.size.height, chunkCoord.z * Chunk.size.width);
    }

    ///////////////////////
    /* Chunks management */
    ///////////////////////

    /* Create/destroy chunks based on view distance */
    void RequestVisibleChunks()
    {
        if (existingChunks == null)
        {
            return;
        }

        Vector3Int currentCoord = new Vector3Int(Mathf.FloorToInt(viewer.position.x / Chunk.size.width), 0, Mathf.FloorToInt(viewer.position.z / Chunk.size.width));

        // Go through world bound difference and delete/mark for generation
        if (viewerCoord != currentCoord) // only if viewer coord is changed
        { 
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
                                                                         : Mathf.Max(currentCoord.z + viewDistance + 1, previousCoord.z - viewDistance));
            regionZ[1] = new Vector3Int(Mathf.Min(currentCoord.x + viewDistance + 1, previousCoord.x + viewDistance + 1), 0,
                                        currentCoord.z > previousCoord.z ? Mathf.Min(currentCoord.z - viewDistance, previousCoord.z + viewDistance + 1)
                                                                         : previousCoord.z + viewDistance + 1);

            // loop through old regions and mark them for regeneration as new
            for (int x = regionX[0].x; x < regionX[1].x; x++)
            {
                for (int z = regionX[0].z; z < regionX[1].z; z++)
                {
                    Chunk tmpChunk;
                    Vector3Int key = new Vector3Int(x, 0, z);
                    if (existingChunks.TryGetValue(key, out tmpChunk))
                    {
                        existingChunks.Remove(key);
                        chunksForRecycling.Enqueue(tmpChunk);
                    }

                    if (Mathf.Abs(currentCoord.x - previousCoord.x) < 2 * viewDistance + 1)
                    {
                        if (currentCoord.x > previousCoord.x)
                            key = key + new Vector3Int(2 * viewDistance + 1, 0, currentCoord.z - previousCoord.z);
                        else
                            key = key + new Vector3Int(-2 * viewDistance - 1, 0, currentCoord.z - previousCoord.z);
                    }
                    else
                    {
                        key = new Vector3Int(x + currentCoord.x - previousCoord.x, 0, z + currentCoord.z - previousCoord.z);
                    }

                    blocksGenerator.RequestData(key);
                }
            }
            for (int z = regionZ[0].z; z < regionZ[1].z; z++)
            {
                for (int x = regionZ[0].x; x < regionZ[1].x; x++)
                {
                    Chunk tmpChunk;
                    Vector3Int key = new Vector3Int(x, 0, z);
                    if (existingChunks.TryGetValue(key, out tmpChunk))
                    {
                        existingChunks.Remove(key);
                        chunksForRecycling.Enqueue(tmpChunk);
                    }

                    if (Mathf.Abs(currentCoord.z - previousCoord.z) < 2 * viewDistance + 1)
                    {
                        if (currentCoord.z > previousCoord.z)
                            key = key + new Vector3Int(0, 0, 2 * viewDistance + 1);
                        else
                            key = key + new Vector3Int(0, 0, -2 * viewDistance - 1);
                    }
                    else
                    {
                        key = new Vector3Int(x, 0, z + currentCoord.z - previousCoord.z);
                    }

                    blocksGenerator.RequestData(key);
                }
            }

            viewerCoord = currentCoord;
        }
    }

    // Recieves map data from generator, assignes it for recycled chunk and triggers mesh generation
    void OnMapDataReceived(GeneratedDataInfo<MapData> mapData)
    {
        if (Mathf.Abs(mapData.coord.x - viewerCoord.x) <= viewDistance &&
            Mathf.Abs(mapData.coord.z - viewerCoord.z) <= viewDistance &&
            !existingChunks.ContainsKey(mapData.coord))
        {
            Chunk chunk = chunksForRecycling.Dequeue();
            chunk.SetBlocks(mapData.data.blocks);
            chunk.SetCoord(mapData.coord);
            existingChunks.Add(mapData.coord, chunk);

            TriggerMeshGeneration(mapData.coord);
        } 
    }       

    // Recieves mesh from generator and requests another one if chunk is dirty
    void OnMeshDataReceived(GeneratedDataInfo<MeshData> meshData)
    {
        Chunk chunk;
        if (existingChunks.TryGetValue(meshData.coord, out chunk))
        {
            chunk.SetMesh(meshData.data);

            if (chunk.isDirty)
            {
                RequestMesh(chunk);
                chunk.SetDirty(false);
            }
            else
                chunk.WaitForMesh(false);
        }    
    }

    // Requests meshes for all adjacent chunks
    void TriggerMeshGeneration(Vector3Int coord)
    {
        Chunk chunk;
        int y = 0;
        for (int x = -1; x <= 1; x++)
            //for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (existingChunks.TryGetValue(new Vector3Int(coord.x + x, coord.y + y, coord.z + z), out chunk))
                        RequestMesh(chunk);
                }
    }

    // Requests mesh or sets chunk dirty if already requested 
    private void MarkForMeshGeneration(Chunk chunk)
    {
        if (chunk.isWaitingMesh)
            chunk.SetDirty(true);
        else
        {
            chunk.WaitForMesh(true);
            RequestMesh(chunk);
        }
    }

    // Checks if mesh is needed and can be generated before requesting it
    void RequestMesh(Chunk chunk)
    {
        if (chunk.hasMesh && !chunk.isWaitingMesh)
            return;

        bool generate = true;

        int y = 0;
        for (int x = -1; x <= 1; x++)
            //for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)                 
                    if (!existingChunks.ContainsKey(new Vector3Int(chunk.coord.x + x, chunk.coord.y + y, chunk.coord.z + z)))
                        generate = false;                 

        if (generate)
            meshGenerator.RequestData(chunk.coord);         
    }


    ////////////////////
    /* Initialization */
    ////////////////////

    void Initialize()
    {
        CreateSingleton();
        SetVariables();       
    }

    void CreateSingleton ()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void SetVariables()
    {   
        viewerCoord = new Vector3Int(0, 0, 0);

        existingChunks = new Dictionary<Vector3Int, Chunk>();
        chunksForRecycling = new Queue<Chunk>();

        blocksGenerator = new BlocksGenerator(blocksGeneratorSettings, OnMapDataReceived, this);
        if (meshGeneratorSettings.useGpu)
            meshGenerator = new GpuMeshGenerator(OnMeshDataReceived, this, meshGeneratorSettings);
        else
            meshGenerator = new CpuMeshGenerator(OnMeshDataReceived, this, meshGeneratorSettings);

        diggingListener = GetComponent<DiggingListener>();

        transform.position = Vector3.zero;

        if (viewer == null)
            viewer = Camera.main.transform;
    }   

    /* Initial generation */
    void CreateChunks ()
    {
        Chunk chunk;
        Vector3Int coord; 

        coord = new Vector3Int(0, 0, 0);
        chunk = CreateChunk();
        chunksForRecycling.Enqueue(chunk);
        blocksGenerator.RequestData(coord);
        for (int i = 1; i <= viewDistance; i++)
            for (int x = 0; x < 2*i; x++)
            {
                coord = new Vector3Int(i, 0, i - x);
                chunk = CreateChunk();
                chunksForRecycling.Enqueue(chunk);
                blocksGenerator.RequestData(coord);

                coord = new Vector3Int(i - x, 0, -i);
                chunk = CreateChunk();
                chunksForRecycling.Enqueue(chunk);
                blocksGenerator.RequestData(coord);

                coord = new Vector3Int(-i, 0, x - i);
                chunk = CreateChunk();
                chunksForRecycling.Enqueue(chunk);
                blocksGenerator.RequestData(coord);

                coord = new Vector3Int(x - i, 0, i);
                chunk = CreateChunk();
                chunksForRecycling.Enqueue(chunk);
                blocksGenerator.RequestData(coord);
            }
    }

    Chunk CreateChunk ()
    {
        GameObject chunk = new GameObject();
        chunk.transform.parent = transform;
        chunk.tag = "Chunk";
        Chunk newChunk = chunk.AddComponent<Chunk>();
        newChunk.Create(meshGeneratorSettings.generateColliders, meshGeneratorSettings.material);
        return newChunk;
    }    

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            if (showBoundsGizmo)
            {
                var chunks = existingChunks.Values;
                foreach (var chunk in chunks)
                {
                    // chunks 
                    if (chunk.hasMesh)
                        Gizmos.color = generatedChunksGizmoCol;
                    else
                        Gizmos.color = chunksGizmoCol;
                    Gizmos.DrawWireCube(ChunkOriginFromCoord(chunk.coord) + Vector3.one * (Chunk.size.width) / 2f, Vector3.one * Chunk.size.width); 
                }

                // world bounds 
                Gizmos.color = Color.green;
                Vector3 worldOrigin = ChunkOriginFromCoord(viewerCoord - new Vector3Int(viewDistance, -1, viewDistance));
                Vector3 worldSize = new Vector3(Chunk.size.width * (2 * viewDistance + 1), 0, Chunk.size.width * (2 * viewDistance + 1));
                Gizmos.DrawLine(worldOrigin, worldOrigin + worldSize - new Vector3(worldSize.x, 0, 0));
                Gizmos.DrawLine(worldOrigin + worldSize - new Vector3(worldSize.x, 0, 0), worldOrigin + worldSize);
                Gizmos.DrawLine(worldOrigin + worldSize, worldOrigin + worldSize - new Vector3(0, 0, worldSize.z));
                Gizmos.DrawLine(worldOrigin + worldSize - new Vector3(0, 0, worldSize.z), worldOrigin);
            }
        }
    }  

    private void OnDestroy()
    {
        meshGenerator.Destroy();
    }
}

// structure for managing generation
public struct GeneratedDataInfo<T>
{
    public readonly T data;
    public readonly Vector3Int coord;

    public GeneratedDataInfo(T data, Vector3Int coord)
    {
        this.coord = coord;
        this.data = data;
    }

}
