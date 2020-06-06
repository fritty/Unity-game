using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Creates and manages chunks */
[DisallowMultipleComponent]
public class ProceduralTerrain : MonoBehaviour
{

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

    // generators
    BlocksGenerator blocksGenerator;
    IMeshGenerator meshGenerator;
    //
    
    public Vector3Int viewerCoord { get; private set; }

    // structures for managing chunks 
    public Dictionary<Vector3Int, Chunk> existingChunks { get; private set; }
    Queue<Chunk> chunksForRecycling;

    GameObject chunkHolder;
    const string chunkHolderName = "Chunks Holder";
    //
    
    Vector3Int generationGizmo;


    void FixedUpdate()
    {
        RequestVisibleChunks();
        blocksGenerator.ManageRequests();
    }

    void LateUpdate()
    {
        meshGenerator.ManageRequests();
    }

    void Awake()
    {
        SetVariables();
        CreateChunkHolder();           
    }      

    void Start()
    {
        CreateChunks();
    }

    /* Interface */

    public byte BlockFromGlobalPosition(Vector3Int position)
    {
        Chunk chunk;
        if (existingChunks.TryGetValue(new Vector3Int(position.x / Chunk.size.width, position.y / Chunk.size.height, position.z / Chunk.size.width), out chunk))
            return chunk.blocks[position.x % Chunk.size.width, position.y % Chunk.size.height, position.z % Chunk.size.width];
        else
            return 0;
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
        if (viewerCoord != currentCoord) // only if coord is changed
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
        generationGizmo = mapData.coord;
    }       

    void OnMeshDataReceived(GeneratedDataInfo<MeshData> meshData)
    {
        Chunk chunk;
        if (existingChunks.TryGetValue(meshData.coord, out chunk))
        {
            chunk.SetMesh(meshData.data);
        }
        generationGizmo = meshData.coord;
    }

    void TriggerMeshGeneration(Vector3Int coord)
    {
        Chunk chunk;
        int y = 0;
        for (int x = -1; x <= 1; x++)
            //for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (existingChunks.TryGetValue(new Vector3Int(coord.x + x, coord.y + y, coord.z + z), out chunk))
                        TryToGenerateMesh(chunk);
                }
    }

    void TryToGenerateMesh(Chunk chunk)
    {
        if (chunk.hasMesh)
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

    void SetVariables()
    {
        generationGizmo = new Vector3Int(0, 0, 0);
        viewerCoord = new Vector3Int(0, 0, 0);

        existingChunks = new Dictionary<Vector3Int, Chunk>();
        chunksForRecycling = new Queue<Chunk>();

        blocksGenerator = new BlocksGenerator(blocksGeneratorSettings, OnMapDataReceived, this);
        if (meshGeneratorSettings.useGpu)
            meshGenerator = new GpuMeshGenerator(OnMeshDataReceived, this, meshGeneratorSettings);
        else
            meshGenerator = new CpuMeshGenerator(OnMeshDataReceived, this, meshGeneratorSettings);

        transform.position = Vector3.zero;

        if (viewer == null)
            viewer = Camera.main.transform;
    }

    /* Create/find chunk holder object for organizing chunks under in the hierarchy */
    void CreateChunkHolder()
    {
        if (chunkHolder == null)
        {
            if (GameObject.Find(chunkHolderName))
            {
                chunkHolder = GameObject.Find(chunkHolderName);
                chunkHolder.transform.SetParent(transform);
            }
            else
            {
                chunkHolder = new GameObject(chunkHolderName);
                chunkHolder.transform.SetParent(transform);
            }
        }
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
        chunk.transform.parent = chunkHolder.transform;
        Chunk newChunk = chunk.AddComponent<Chunk>();
        newChunk.Create(meshGeneratorSettings.generateColliders, meshGeneratorSettings.material);
        return newChunk;
    }    

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            var chunks = existingChunks.Values;
            foreach (var chunk in chunks)
            {
                // chunks            
                if (showBoundsGizmo)
                {
                    if (chunk.hasMesh)
                        Gizmos.color = generatedChunksGizmoCol;
                    else
                        Gizmos.color = chunksGizmoCol;
                    Gizmos.DrawWireCube(OriginFromCoord(chunk.coord) + Vector3.one * (Chunk.size.width) / 2f, Vector3.one * Chunk.size.width);
                }
            }

            // world bounds
            if (showBoundsGizmo)
            {
                Gizmos.color = Color.red; // currently generated chunk
                Gizmos.DrawWireCube(OriginFromCoord(generationGizmo) + Vector3.one * (Chunk.size.width) / 2f, Vector3.one * Chunk.size.width);

                Gizmos.color = Color.green;
                Vector3 worldOrigin = OriginFromCoord(viewerCoord - new Vector3Int(viewDistance, -1, viewDistance));
                Vector3 worldSize = new Vector3(Chunk.size.width * (2 * viewDistance + 1), 0, Chunk.size.width * (2 * viewDistance + 1));
                Gizmos.DrawLine(worldOrigin, worldOrigin + worldSize - new Vector3(worldSize.x, 0, 0));
                Gizmos.DrawLine(worldOrigin + worldSize - new Vector3(worldSize.x, 0, 0), worldOrigin + worldSize);
                Gizmos.DrawLine(worldOrigin + worldSize, worldOrigin + worldSize - new Vector3(0, 0, worldSize.z));
                Gizmos.DrawLine(worldOrigin + worldSize - new Vector3(0, 0, worldSize.z), worldOrigin);
            }
        }
    }

    Vector3 OriginFromCoord(Vector3Int coord)
    {
        return new Vector3(coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
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
