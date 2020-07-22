using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Sandbox.ProceduralTerrain.Core
{
    // Jobs and structures for CPU mesh generation

    [BurstCompile]
    // parallel job for calculating vertex values
    public struct FillVerticiesArrayJob : IJobParallelFor
    {
        [ReadOnly]
        public ChunkMarchBlocks blocks; // (w+3)(h+3)(w+3)
        [WriteOnly]
        public ChunkMarchVerticies verticesExpanded; // [3]x(w+1)(h+1)(w+1) 

        public void Execute(int id) // 0 <= id < (w+1)(h+1)(w+1)
        {
            CalculateVerticies(id);
        }

        void CalculateVerticies(int id)
        {
            // convert id into coordinates        
            int3 vecId = verticesExpanded.IdToCoord(id);

            Byte3 vert = new Byte3(0, 0, 0);

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
        public ChunkMarchBlocks blocks; // (w+3)(h+3)(w+3)
        [ReadOnly]
        public ChunkMarchVerticies verticiesExpanded; // [3]x(w+1)(h+1)(w+1)
        [ReadOnly]
        public MarchTablesBurst marchTables;

        [WriteOnly]
        public NativeQueue<float3> verticies;
        [WriteOnly]
        public NativeQueue<int> indicies;

        //public NativeArray<int> mapping;


        public void Execute()
        {
            // array for storing corresponding indicies
            NativeArray<int> mapping = new NativeArray<int>(ChunkMarchVerticies.Length * 3, Allocator.Temp, NativeArrayOptions.ClearMemory);
            int mappingValue = 1;

            for (int cubeIndex = 0; cubeIndex < ChunkMarchBlocks.Length; cubeIndex++)
            {
                int3 cubeCoord = blocks.ChunkBlockIdToPosition(cubeIndex);
                byte cubeConfiguration = CubeConfigurationFromCoord(cubeCoord);

                // exceptions for smooth transitions
                if (cubeConfiguration == 15 || cubeConfiguration == 51 || cubeConfiguration == 102 || cubeConfiguration == 153 || cubeConfiguration == 204 || cubeConfiguration == 240)
                {
                    float3 median_1, median_2;
                    int triangulationIndex = cubeConfiguration * marchTables.MaxTriIndex;
                    int2 vertId_1 = VertexIdFromEdge(marchTables.Triangulation[triangulationIndex + 0], cubeCoord);
                    int2 vertId_2 = VertexIdFromEdge(marchTables.Triangulation[triangulationIndex + 1], cubeCoord);
                    int2 vertId_3 = VertexIdFromEdge(marchTables.Triangulation[triangulationIndex + 2], cubeCoord);
                    int2 vertId_4 = VertexIdFromEdge(marchTables.Triangulation[triangulationIndex + 5], cubeCoord);
                    float3 vertex_1 = VertexVector(vertId_1);
                    float3 vertex_2 = VertexVector(vertId_2);
                    float3 vertex_3 = VertexVector(vertId_3);
                    float3 vertex_4 = VertexVector(vertId_4);

                    median_1 = (vertex_2 + vertex_3) / 2f;
                    median_2 = (vertex_1 + vertex_4) / 2f;

                    if ((cubeConfiguration == 153 && median_1.x >= median_2.x) ||
                        (cubeConfiguration == 102 && median_1.x < median_2.x) ||
                        (cubeConfiguration == 15 && median_1.y >= median_2.y) ||
                        (cubeConfiguration == 240 && median_1.y < median_2.y) ||
                        (cubeConfiguration == 51 && median_1.z >= median_2.z) ||
                        (cubeConfiguration == 204 && median_1.z < median_2.z))
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
                    for (int triIndex = 0; marchTables.Triangulation[cubeConfiguration * marchTables.MaxTriIndex + triIndex] != 255; triIndex += 3)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            int2 vertexId = VertexIdFromEdge(marchTables.Triangulation[cubeConfiguration * marchTables.MaxTriIndex + triIndex + j], cubeCoord);

                            EnqueueResult(vertexId, VertexVector(vertexId), ref mappingValue, mapping);
                        }
                    }
                }
            }

            mapping.Dispose();
        }

        void EnqueueResult(int2 vertexId, float3 vertex, ref int mappingValue, NativeArray<int> mapping)
        {
            int mappingId = vertexId.x * 3 + vertexId.y;

            if (mapping[mappingId] == 0)
            {
                mapping[mappingId] = mappingValue++;
                verticies.Enqueue(vertex);
            }

            indicies.Enqueue(mapping[mappingId] - 1);
        }

        int2 VertexIdFromEdge(byte edge, int3 cubeCoord)
        {
            if (edge == 0) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)), 0);
            if (edge == 8) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)), 1);
            if (edge == 3) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)), 2);

            if (edge == 9) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y, cubeCoord.z)), 1);
            if (edge == 1) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y, cubeCoord.z)), 2);
            if (edge == 4) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y + 1, cubeCoord.z)), 0);
            if (edge == 7) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y + 1, cubeCoord.z)), 2);
            if (edge == 5) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y + 1, cubeCoord.z)), 2);
            if (edge == 2) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z + 1)), 0);
            if (edge == 11) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z + 1)), 1);
            if (edge == 10) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x + 1, cubeCoord.y, cubeCoord.z + 1)), 1);
            if (edge == 6) return new int2(verticiesExpanded.CoordToId(new int3(cubeCoord.x, cubeCoord.y + 1, cubeCoord.z + 1)), 0);

            return 0;
        }

        byte CubeConfigurationFromCoord(int3 cubeCoord)
        {
            byte configuration = 0;
            if (blocks[new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z)] > 0) configuration |= 1;
            if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y, cubeCoord.z)] > 0) configuration |= 2;
            if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y, cubeCoord.z + 1)] > 0) configuration |= 4;
            if (blocks[new int3(cubeCoord.x, cubeCoord.y, cubeCoord.z + 1)] > 0) configuration |= 8;
            if (blocks[new int3(cubeCoord.x, cubeCoord.y + 1, cubeCoord.z)] > 0) configuration |= 16;
            if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y + 1, cubeCoord.z)] > 0) configuration |= 32;
            if (blocks[new int3(cubeCoord.x + 1, cubeCoord.y + 1, cubeCoord.z + 1)] > 0) configuration |= 64;
            if (blocks[new int3(cubeCoord.x, cubeCoord.y + 1, cubeCoord.z + 1)] > 0) configuration |= 128;

            return configuration;
        }

        float3 VertexVector(int2 vertId)
        {
            float3 delta = new float3((vertId.y == 0) ? verticiesExpanded[vertId.x].x * 16 / 4081f : 0f, // !shrinking cube to preserve vertex id information
                                      (vertId.y == 1) ? verticiesExpanded[vertId.x].y * 16 / 4081f : 0f, // will remove later
                                      (vertId.y == 2) ? verticiesExpanded[vertId.x].z * 16 / 4081f : 0f);
            int3 vertCoord = verticiesExpanded.IdToCoord(vertId.x);
            return new float3(vertCoord.x + delta.x, vertCoord.y + delta.y, vertCoord.z + delta.z);
        }
    }

    public struct InclineCondition
    {
        public static bool Check(byte offset_N, byte offset_P, byte offset_2N, byte offset_2P)
        {
            bool a = (offset_P != 0) && (offset_N == 0);
            bool b = (offset_N != 0) && (offset_P == 0);

            return (a || b) && ((offset_2N == 0) || b) && ((offset_2P == 0) || a);
        }

        public static float Evaluate(byte offset_N, byte offset_P, byte corner)
        {
            if (offset_N > 0) return 255f * corner / (255f - offset_N + corner);
            return 255f * corner / (255f - offset_P + corner);
        }
    }
}