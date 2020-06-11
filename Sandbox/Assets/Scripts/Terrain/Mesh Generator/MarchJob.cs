using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

// Jobs and structures for CPU mesh generation

[BurstCompile]
// parallel job for calculating vertex values
public struct FillVerticiesArrayJob : IJobParallelFor
{   
    [ReadOnly]
    public ChunkMeshBlocks blocks; // (w+3)(h+3)(w+3)
    [WriteOnly]
    public ChunkMeshVerticies verticesExpanded; // [3]x(w+1)(h+1)(w+1) 

    public void Execute (int id) // 0 <= id < (w+1)(h+1)(w+1)
    {   
        CalculateVerticies(id);
    }
          
    void CalculateVerticies (int id)
    {
        // convert id into coordinates        
        int3 vecId = verticesExpanded.IdToCoord(id);

        Byte3 vert = new Byte3(0,0,0);

        // for each coordinate calculate 3 potential vertices
        vert.x = VertexFromDirection(new int3(1, 0, 0), vecId);
        vert.y = VertexFromDirection(new int3(0, 1, 0), vecId);
        vert.z = VertexFromDirection(new int3(0, 0, 1), vecId); 

        verticesExpanded[id] = vert;
    }

    // vertex value for a given direction
    byte VertexFromDirection(int3 direction, int3 vecId)
    {  
        // corners that form a given edge
        byte corner_A = blocks[vecId];
        byte corner_B = blocks[vecId + direction];

        if (corner_A > 0 ^ corner_B > 0) // calculate vertex only if one of the corners is 0
        {
            byte vertex, corner;
            
            if (corner_A > 0)
                corner = corner_A;
            else    
            {
                corner = corner_B;
                direction = -direction;
            }

            vertex = corner;
            vertex = (byte)math.max(InclinedVertexFromOffset(new int3(1, 0, 0), direction, vecId, corner), (uint)vertex);
            vertex = (byte)math.max(InclinedVertexFromOffset(new int3(0, 1, 0), direction, vecId, corner), (uint)vertex);
            vertex = (byte)math.max(InclinedVertexFromOffset(new int3(0, 0, 1), direction, vecId, corner), (uint)vertex); 

            return (corner_A > 0) ? vertex : (byte)(255 - vertex);      
        }

        return 0;
    } 

    // vertex value dependent on local incline
    byte InclinedVertexFromOffset(int3 offset, int3 direction, int3 vecId, byte corner)
    {     
        if (math.abs(direction).Equals(offset))
            return 0;

        int3 directionOffset = !math.abs(direction).Equals(direction) ? new int3(0, 0, 0) : direction;

        byte offset_P = blocks[vecId + directionOffset + offset];
        byte offset_N = blocks[vecId + directionOffset - offset];
        byte offset_2P = blocks[vecId + directionOffset + offset * 2];
        byte offset_2N = blocks[vecId + directionOffset - offset * 2];

        if (InclineCondition.Check(offset_N, offset_P, offset_2N, offset_2P)) // incline condition
        {
            return (byte)InclineCondition.Evaluate(offset_N, offset_P, corner);
        }

        return 0;
    } 
}

[BurstCompile]
// normal job for forming mesh data
public struct CollapseIndiciesJob : IJob
{  
    [ReadOnly]
    public ChunkMeshBlocks blocks; // (w+3)(h+3)(w+3)
    [ReadOnly]
    public ChunkMeshVerticies verticiesExpanded; // [3]x(w+1)(h+1)(w+1)
    [ReadOnly]
    public MarchTablesBurst marchTables;

    [WriteOnly]
    public NativeQueue<float3> verticies;
    [WriteOnly]
    public NativeQueue<int> indicies; 


    public void Execute ()
    {
        // array for storing corresponding indicies
        NativeArray<int> mapping = new NativeArray<int>(verticiesExpanded.length * 3, Allocator.Temp, NativeArrayOptions.ClearMemory); 
        int mappingValue = 1; 

        for (int cubeIndex = 0; cubeIndex < blocks.chunkWidth * blocks.chunkWidth * blocks.chunkHeight; cubeIndex++)
        {
            int3 cubeCoord = blocks.ChunkBlockIdToCoord(cubeIndex);   
            byte cubeConfiguration = CubeConfigurationFromCoord(cubeCoord);

                // exceptions for smooth transitions
            if (cubeConfiguration == 15 || cubeConfiguration == 51 || cubeConfiguration == 102 || cubeConfiguration == 153 || cubeConfiguration == 204 || cubeConfiguration == 240)
            {   
                float3 median_1, median_2;                 
                int triangulationIndex = cubeConfiguration * marchTables.maxTriIndex;
                int2 vertId_1 = VertexIdFromEdge(marchTables.triangulation[triangulationIndex + 0], cubeCoord);
                int2 vertId_2 = VertexIdFromEdge(marchTables.triangulation[triangulationIndex + 1], cubeCoord);
                int2 vertId_3 = VertexIdFromEdge(marchTables.triangulation[triangulationIndex + 2], cubeCoord);
                int2 vertId_4 = VertexIdFromEdge(marchTables.triangulation[triangulationIndex + 5], cubeCoord); 
                float3 vertex_1 = VertexVector(vertId_1);
                float3 vertex_2 = VertexVector(vertId_2);
                float3 vertex_3 = VertexVector(vertId_3);
                float3 vertex_4 = VertexVector(vertId_4);

                median_1 = (vertex_2 + vertex_3) / 2f;
                median_2 = (vertex_1 + vertex_4) / 2f;                

                if ((cubeConfiguration == 153 && median_1.x >= median_2.x) ||
                    (cubeConfiguration == 102 && median_1.x <  median_2.x) ||
                    (cubeConfiguration == 15  && median_1.y >= median_2.y) ||
                    (cubeConfiguration == 240 && median_1.y <  median_2.y) ||
                    (cubeConfiguration == 51  && median_1.z >= median_2.z) ||
                    (cubeConfiguration == 204 && median_1.z <  median_2.z)) 
                {
                    EnqueueResult(vertId_1, vertex_1, ref mappingValue, mapping);
                    EnqueueResult(vertId_2, vertex_2, ref mappingValue, mapping);
                    EnqueueResult(vertId_3, vertex_3, ref mappingValue, mapping);

                    EnqueueResult(vertId_3, vertex_3, ref mappingValue, mapping);
                    EnqueueResult(vertId_2, vertex_2, ref mappingValue, mapping);
                    EnqueueResult(vertId_4, vertex_4, ref mappingValue, mapping);
                }
                else
                {
                    EnqueueResult(vertId_1, vertex_1, ref mappingValue, mapping);
                    EnqueueResult(vertId_2, vertex_2, ref mappingValue, mapping);
                    EnqueueResult(vertId_4, vertex_4, ref mappingValue, mapping);

                    EnqueueResult(vertId_1, vertex_1, ref mappingValue, mapping);
                    EnqueueResult(vertId_4, vertex_4, ref mappingValue, mapping);
                    EnqueueResult(vertId_3, vertex_3, ref mappingValue, mapping);
                }  
            }
            else // other cases
            {
                for (int triIndex = 0; marchTables.triangulation[cubeConfiguration * marchTables.maxTriIndex + triIndex] != 255; triIndex += 3)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int2 vertexId = VertexIdFromEdge(marchTables.triangulation[cubeConfiguration * marchTables.maxTriIndex + triIndex + j], cubeCoord);

                        EnqueueResult(vertexId, VertexVector(vertexId), ref mappingValue, mapping); 
                    }
                }
            } 
        }

        mapping.Dispose();
    }

    void EnqueueResult (int2 vertexId, float3 vertex, ref int mappingValue, NativeArray<int> mapping)
    {
        int mappingId = vertexId.x * 3 + vertexId.y;

        if (mapping[mappingId] == 0)
        {
            mapping[mappingId] = mappingValue++;
            verticies.Enqueue(vertex);
        }

        indicies.Enqueue(mapping[mappingId] - 1);
    }

    int2 VertexIdFromEdge (byte edge, int3 cubeCoord)
    {
        if (edge == 0) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)), 0);
        if (edge == 8) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)), 1);
        if (edge == 3) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)), 2);

        if (edge == 9)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y    , cubeCoord.z    )), 1);
        if (edge == 1)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y    , cubeCoord.z    )), 2);         
        if (edge == 4)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x    , cubeCoord.y + 1, cubeCoord.z    )), 0);
        if (edge == 7)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x    , cubeCoord.y + 1, cubeCoord.z    )), 2);
        if (edge == 5)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y + 1, cubeCoord.z    )), 2);
        if (edge == 2)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x    , cubeCoord.y    , cubeCoord.z + 1)), 0);
        if (edge == 11) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x    , cubeCoord.y    , cubeCoord.z + 1)), 1);
        if (edge == 10) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y    , cubeCoord.z + 1)), 1);
        if (edge == 6)  return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x    , cubeCoord.y + 1, cubeCoord.z + 1)), 0);

        return 0;
    } 

    byte CubeConfigurationFromCoord (int3 cubeCoord)
    {     
        byte configuration = 0;
        if (blocks[new int3(cubeCoord.x    , cubeCoord.y    , cubeCoord.z    )] > 0) configuration |= 1;
        if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y    , cubeCoord.z    )] > 0) configuration |= 2;
        if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y    , cubeCoord.z + 1)] > 0) configuration |= 4;
        if (blocks[new int3(cubeCoord.x    , cubeCoord.y    , cubeCoord.z + 1)] > 0) configuration |= 8;
        if (blocks[new int3(cubeCoord.x    , cubeCoord.y + 1, cubeCoord.z    )] > 0) configuration |= 16;
        if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y + 1, cubeCoord.z    )] > 0) configuration |= 32;
        if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y + 1, cubeCoord.z + 1)] > 0) configuration |= 64;
        if (blocks[new int3(cubeCoord.x    , cubeCoord.y + 1, cubeCoord.z + 1)] > 0) configuration |= 128;

        return configuration;        
    }

    float3 VertexVector (int2 vertId)
    {       
        float3 delta = new float3((vertId.y == 0) ? verticiesExpanded[vertId.x].x / 256f : 0f, // ! shrinking cube by a factor of 1/256 to make sure vertices preserve edge information
                                  (vertId.y == 1) ? verticiesExpanded[vertId.x].y / 256f : 0f,
                                  (vertId.y == 2) ? verticiesExpanded[vertId.x].z / 256f : 0f); 
        int3 vertCoord = verticiesExpanded.IdToCoord(vertId.x);
        return new float3(vertCoord.x + delta.x, vertCoord.y + delta.y, vertCoord.z + delta.z);
    }
}

public struct InclineCondition
{
    public static bool Check (byte offset_N, byte offset_P, byte offset_2N, byte offset_2P)
    {
        bool a = (offset_P != 0) && (offset_N == 0);
        bool b = (offset_N != 0) && (offset_P == 0);

        return (a || b) && ((offset_2N == 0) || b) && ((offset_2P == 0) || a);
    }

    public static float Evaluate (byte offset_N, byte offset_P, byte corner)
    {
        if (offset_N > 0) return 255f * corner / (255f - offset_N + corner);
        return 255f * corner / (255f - offset_P + corner);
    }
}

public struct Byte3
{
    public byte x, y, z;

    public Byte3 (byte x, byte y, byte z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public byte this[int i]
    {
        get
        {
            switch (i)
            {
                case 0: return x;
                case 1: return y;
                default: return z;
            }
        } 
        set
        {
            switch (i)
            {
                case 0: x = value; return;
                case 1: y = value; return;
                default: z = value; return;
            }
        }      
    }
}

public struct ChunkMeshVerticies
{
    public NativeArray<Byte3> verticies;
    public int width { get; }
    public int height { get; }
    public int length { get; }

    public ChunkMeshVerticies (int chunkWidth, int chunkHeight)
    {
        width = chunkWidth + 1;
        height = chunkHeight + 1;
        length = width * width * height;
        verticies = new NativeArray<Byte3>(length, Allocator.Persistent);
    }

    public int3 IdToCoord (int id)
    {
        return new int3(id % width, (id / width) % height, id / (width * height));
    }

    public int CoordToId (int3 coordinate)
    {
        return coordinate.x + coordinate.y * width + coordinate.z * width * height;
    }

    public Byte3 this[int i]
    {
        get
        {
            return verticies[i];
        }
        set
        {
            verticies[i] = value;
        }
    }

    public Byte3 this[int3 coordinate]
    {           
        get
        {
            int id = coordinate.x + coordinate.y * width + coordinate.z * width * height;
            if (id >= length) return new Byte3(0, 0, 0);
            return verticies[id];
        }
        set
        {
            int id = coordinate.x + coordinate.y * width + coordinate.z * width * height;
            if (id < length)
                verticies[id] = value;
        }
    }
}

public struct ChunkMeshBlocks
{
    public NativeArray<byte> blocks;
    public int width { get; }
    public int height { get; }
    public int length { get; }
    public int chunkWidth { get; }
    public int chunkHeight { get; }
    public const int lowBoundaryOffset = 2;
    public const int highBoundaryOffset = 3;

    public ChunkMeshBlocks(int chunkWidth, int chunkHeight)
    {
        this.chunkWidth = chunkWidth;
        this.chunkHeight = chunkHeight; 
        width = chunkWidth + lowBoundaryOffset + highBoundaryOffset;
        height = chunkHeight + lowBoundaryOffset + highBoundaryOffset;
        length = width * width * height;
        blocks = new NativeArray<byte>(length, Allocator.Persistent);          
    } 

    public int3 ChunkBlockIdToCoord(int id)
    {
        return new int3(id % chunkWidth, 
                       (id / chunkWidth) % chunkHeight, 
                        id / (chunkWidth * chunkHeight));
    } 

    public byte this[int i]
    {
        get
        {
            return blocks[i];
        }
        set
        {
            blocks[i] = value;
        }
    }

    public byte this[int3 coordinate]
    {
        get
        {
            int id = (coordinate.x + lowBoundaryOffset) + (coordinate.y + lowBoundaryOffset) * width + (coordinate.z + lowBoundaryOffset) * width * height;
            if (id >= length || id < 0) return 0;
            return blocks[id];
        }
        set
        {
            int id = (coordinate.x + lowBoundaryOffset) + (coordinate.y + lowBoundaryOffset) * width + (coordinate.z + lowBoundaryOffset) * width * height;
            if (id < length)
                blocks[id] = value;
        }
    }
}

public struct MarchTablesBurst
{
    public NativeArray<byte> triangulation;
    public int maxConfiguration { get; }
    public int maxTriIndex { get; }
    public NativeArray<byte> cornerIndexAFromEdge;
    public NativeArray<byte> cornerIndexBFromEdge;

    public MarchTablesBurst (byte x)
    {
        maxConfiguration = 256;
        maxTriIndex = 16;
        triangulation = new NativeArray<byte>(maxConfiguration * maxTriIndex, Allocator.Persistent);
        for (int configuration = 0; configuration < maxConfiguration; configuration++)
            for (int triIndex = 0; triIndex < maxTriIndex; triIndex++)
            {
                triangulation[configuration * maxTriIndex + triIndex] = MarchTables.triangulation[configuration, triIndex];
            }
        cornerIndexAFromEdge = new NativeArray<byte>(12, Allocator.Persistent);
        cornerIndexBFromEdge = new NativeArray<byte>(12, Allocator.Persistent);
        for (int i = 0; i < 12; i++)
        {
            cornerIndexAFromEdge[i] = MarchTables.cornerIndexAFromEdge[i];
            cornerIndexBFromEdge[i] = MarchTables.cornerIndexBFromEdge[i];
        }
    }

    public void Dispose()
    {
        if (triangulation.IsCreated)
            triangulation.Dispose();
        if (cornerIndexAFromEdge.IsCreated)
            cornerIndexAFromEdge.Dispose();
        if (cornerIndexBFromEdge.IsCreated)
            cornerIndexBFromEdge.Dispose();
    }
} 

struct MarchTables { 
    public static byte[,] triangulation = {
        {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 8, 3, 9, 8, 1, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 1, 2, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 2, 10, 0, 2, 9, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 8, 3, 2, 10, 8, 10, 9, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 11, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 11, 2, 8, 11, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 9, 0, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 11, 2, 1, 9, 11, 9, 8, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 10, 1, 11, 10, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 10, 1, 0, 8, 10, 8, 11, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 9, 0, 3, 11, 9, 11, 10, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 8, 10, 10, 8, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 7, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 3, 0, 7, 3, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 1, 9, 4, 7, 1, 7, 3, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 4, 7, 3, 0, 4, 1, 2, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 2, 10, 9, 0, 2, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, 255, 255, 255, 255 },
        { 8, 4, 7, 3, 11, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 4, 7, 11, 2, 4, 2, 0, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 0, 1, 8, 4, 7, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, 255, 255, 255, 255 },
        { 3, 10, 1, 3, 11, 10, 7, 8, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, 255, 255, 255, 255 },
        { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, 255, 255, 255, 255 },
        { 4, 7, 11, 4, 11, 9, 9, 11, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 4, 0, 8, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 5, 4, 1, 5, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 5, 4, 8, 3, 5, 3, 1, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 9, 5, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 8, 1, 2, 10, 4, 9, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 2, 10, 5, 4, 2, 4, 0, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, 255, 255, 255, 255 },
        { 9, 5, 4, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 11, 2, 0, 8, 11, 4, 9, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 5, 4, 0, 1, 5, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, 255, 255, 255, 255 },
        { 10, 3, 11, 10, 1, 3, 9, 5, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, 255, 255, 255, 255 },
        { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, 255, 255, 255, 255 },
        { 5, 4, 8, 5, 8, 10, 10, 8, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 7, 8, 5, 7, 9, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 3, 0, 9, 5, 3, 5, 7, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 7, 8, 0, 1, 7, 1, 5, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 5, 3, 3, 5, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 7, 8, 9, 5, 7, 10, 1, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, 255, 255, 255, 255 },
        { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, 255, 255, 255, 255 },
        { 2, 10, 5, 2, 5, 3, 3, 5, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 9, 5, 7, 8, 9, 3, 11, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, 255, 255, 255, 255 },
        { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, 255, 255, 255, 255 },
        { 11, 2, 1, 11, 1, 7, 7, 1, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, 255, 255, 255, 255 },
        { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, 255 },
        { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, 255 },
        { 11, 10, 5, 7, 11, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 6, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 0, 1, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 8, 3, 1, 9, 8, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 6, 5, 2, 6, 1, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 6, 5, 1, 2, 6, 3, 0, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 6, 5, 9, 0, 6, 0, 2, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, 255, 255, 255, 255 },
        { 2, 3, 11, 10, 6, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 0, 8, 11, 2, 0, 10, 6, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 2, 3, 11, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, 255, 255, 255, 255 },
        { 6, 3, 11, 6, 5, 3, 5, 1, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, 255, 255, 255, 255 },
        { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, 255, 255, 255, 255 },
        { 6, 5, 9, 6, 9, 11, 11, 9, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 10, 6, 4, 7, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 3, 0, 4, 7, 3, 6, 5, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 9, 0, 5, 10, 6, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, 255, 255, 255, 255 },
        { 6, 1, 2, 6, 5, 1, 4, 7, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, 255, 255, 255, 255 },
        { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, 255, 255, 255, 255 },
        { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, 255 },
        { 3, 11, 2, 7, 8, 4, 10, 6, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, 255, 255, 255, 255 },
        { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, 255, 255, 255, 255 },
        { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, 255 },
        { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, 255, 255, 255, 255 },
        { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, 255 },
        { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, 255 },
        { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, 255, 255, 255, 255 },
        { 10, 4, 9, 6, 4, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 10, 6, 4, 9, 10, 0, 8, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 0, 1, 10, 6, 0, 6, 4, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, 255, 255, 255, 255 },
        { 1, 4, 9, 1, 2, 4, 2, 6, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, 255, 255, 255, 255 },
        { 0, 2, 4, 4, 2, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 3, 2, 8, 2, 4, 4, 2, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 4, 9, 10, 6, 4, 11, 2, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, 255, 255, 255, 255 },
        { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, 255, 255, 255, 255 },
        { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, 255 },
        { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, 255, 255, 255, 255 },
        { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, 255 },
        { 3, 11, 6, 3, 6, 0, 0, 6, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 4, 8, 11, 6, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 10, 6, 7, 8, 10, 8, 9, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, 255, 255, 255, 255 },
        { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, 255, 255, 255, 255 },
        { 10, 6, 7, 10, 7, 1, 1, 7, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, 255, 255, 255, 255 },
        { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, 255 },
        { 7, 8, 0, 7, 0, 6, 6, 0, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 3, 2, 6, 7, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, 255, 255, 255, 255 },
        { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, 255 },
        { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, 255 },
        { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, 255, 255, 255, 255 },
        { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, 255 },
        { 0, 9, 1, 11, 6, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, 255, 255, 255, 255 },
        { 7, 11, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 6, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 8, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 1, 9, 8, 3, 1, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 1, 2, 6, 11, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 3, 0, 8, 6, 11, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 9, 0, 2, 10, 9, 6, 11, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, 255, 255, 255, 255 },
        { 7, 2, 3, 6, 2, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 0, 8, 7, 6, 0, 6, 2, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 7, 6, 2, 3, 7, 0, 1, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, 255, 255, 255, 255 },
        { 10, 7, 6, 10, 1, 7, 1, 3, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, 255, 255, 255, 255 },
        { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, 255, 255, 255, 255 },
        { 7, 6, 10, 7, 10, 8, 8, 10, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 8, 4, 11, 8, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 6, 11, 3, 0, 6, 0, 4, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 6, 11, 8, 4, 6, 9, 0, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, 255, 255, 255, 255 },
        { 6, 8, 4, 6, 11, 8, 2, 10, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, 255, 255, 255, 255 },
        { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, 255, 255, 255, 255 },
        { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, 255 },
        { 8, 2, 3, 8, 4, 2, 4, 6, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 4, 2, 2, 4, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, 255, 255, 255, 255 },
        { 1, 9, 4, 1, 4, 2, 2, 4, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, 255, 255, 255, 255 },
        { 10, 1, 0, 10, 0, 6, 6, 0, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, 255 },
        { 10, 9, 4, 6, 10, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 5, 7, 6, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 4, 9, 5, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 0, 1, 5, 4, 0, 7, 6, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, 255, 255, 255, 255 },
        { 9, 5, 4, 10, 1, 2, 7, 6, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, 255, 255, 255, 255 },
        { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, 255, 255, 255, 255 },
        { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, 255 },
        { 7, 2, 3, 7, 6, 2, 5, 4, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, 255, 255, 255, 255 },
        { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, 255, 255, 255, 255 },
        { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, 255 },
        { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, 255, 255, 255, 255 },
        { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, 255 },
        { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, 255 },
        { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, 255, 255, 255, 255 },
        { 6, 9, 5, 6, 11, 9, 11, 8, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, 255, 255, 255, 255 },
        { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, 255, 255, 255, 255 },
        { 6, 11, 3, 6, 3, 5, 5, 3, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, 255, 255, 255, 255 },
        { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, 255 },
        { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, 255 },
        { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, 255, 255, 255, 255 },
        { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, 255, 255, 255, 255 },
        { 9, 5, 6, 9, 6, 0, 0, 6, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, 255 },
        { 1, 5, 6, 2, 1, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, 255 },
        { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, 255, 255, 255, 255 },
        { 0, 3, 8, 5, 6, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 5, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 5, 10, 7, 5, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 5, 10, 11, 7, 5, 8, 3, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 11, 7, 5, 10, 11, 1, 9, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, 255, 255, 255, 255 },
        { 11, 1, 2, 11, 7, 1, 7, 5, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, 255, 255, 255, 255 },
        { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, 255, 255, 255, 255 },
        { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, 255 },
        { 2, 5, 10, 2, 3, 5, 3, 7, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, 255, 255, 255, 255 },
        { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, 255, 255, 255, 255 },
        { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, 255 },
        { 1, 3, 5, 5, 3, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 7, 0, 7, 1, 1, 7, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 0, 3, 9, 3, 5, 5, 3, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 8, 7, 5, 9, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 8, 4, 5, 10, 8, 10, 11, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, 255, 255, 255, 255 },
        { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, 255, 255, 255, 255 },
        { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, 255 },
        { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, 255, 255, 255, 255 },
        { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, 255 },
        { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, 255 },
        { 9, 4, 5, 2, 11, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, 255, 255, 255, 255 },
        { 5, 10, 2, 5, 2, 4, 4, 2, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, 255 },
        { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, 255, 255, 255, 255 },
        { 8, 4, 5, 8, 5, 3, 3, 5, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 4, 5, 1, 0, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, 255, 255, 255, 255 },
        { 9, 4, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 11, 7, 4, 9, 11, 9, 10, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, 255, 255, 255, 255 },
        { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, 255, 255, 255, 255 },
        { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, 255 },
        { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, 255, 255, 255, 255 },
        { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, 255 },
        { 11, 7, 4, 11, 4, 2, 2, 4, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, 255, 255, 255, 255 },
        { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, 255, 255, 255, 255 },
        { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, 255 },
        { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, 255 },
        { 1, 10, 2, 8, 7, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 1, 4, 1, 7, 7, 1, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, 255, 255, 255, 255 },
        { 4, 0, 3, 7, 4, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 8, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 10, 8, 8, 10, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 9, 3, 9, 11, 11, 9, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 10, 0, 10, 8, 8, 10, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 1, 10, 11, 3, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 11, 1, 11, 9, 9, 11, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, 255, 255, 255, 255 },
        { 0, 2, 11, 8, 0, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 2, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 3, 8, 2, 8, 10, 10, 8, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 10, 2, 0, 9, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, 255, 255, 255, 255 },
        { 1, 10, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 3, 8, 9, 1, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 9, 1, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 3, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }
        };

    public static byte[] cornerIndexAFromEdge = {
        0,
        1,
        5,
        4,
        2,
        3,
        7,
        6,
        0,
        1,
        5,
        4
    };

    public static byte[] cornerIndexBFromEdge = {
        1,
        5,
        4,
        0,
        3,
        7,
        6,
        2,
        2,
        3,
        7,
        6
    };
}