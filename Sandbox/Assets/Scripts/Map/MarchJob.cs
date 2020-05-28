using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

// IJob for CPU mesh generation
// public struct MeshGenJob : IJobParallelFor
// {
//     public NativeArray<byte> blocks;

//     public void Execute (int id) {
//         Vector3Int newId = new Vector3Int();
//         March(newId);
//     }

//     void March (Vector3Int id) {   
//         // error check
//         if (id.x > Chunk.size.width-1 || id.y >= Chunk.size.height-1 || id.z > Chunk.size.width-1) 
//             return;

//         byte[] cubeCorners = new byte[8];
//         // 8 corners of the current cube
//         // if (id.x < Chunk.size.width-1 && id.z < Chunk.size.width-1)
//         // {        
//         //     cubeCorners[0] = valueFromCoord(id.x    , id.y    , id.z    );
//         //     cubeCorners[1] = valueFromCoord(id.x + 1, id.y    , id.z    );
//         //     cubeCorners[2] = valueFromCoord(id.x    , id.y + 1, id.z    );
//         //     cubeCorners[3] = valueFromCoord(id.x + 1, id.y + 1, id.z    );
//         //     cubeCorners[4] = valueFromCoord(id.x    , id.y    , id.z + 1);
//         //     cubeCorners[5] = valueFromCoord(id.x + 1, id.y    , id.z + 1);
//         //     cubeCorners[6] = valueFromCoord(id.x    , id.y + 1, id.z + 1);
//         //     cubeCorners[7] = valueFromCoord(id.x + 1, id.y + 1, id.z + 1);
//         // }
//         // else
//         // {        
//         //     cubeCorners[0] = edgeValueFromCoord(id.x    , id.y    , id.z    );
//         //     cubeCorners[1] = edgeValueFromCoord(id.x + 1, id.y    , id.z    );
//         //     cubeCorners[2] = edgeValueFromCoord(id.x    , id.y + 1, id.z    );
//         //     cubeCorners[3] = edgeValueFromCoord(id.x + 1, id.y + 1, id.z    );
//         //     cubeCorners[4] = edgeValueFromCoord(id.x    , id.y    , id.z + 1);
//         //     cubeCorners[5] = edgeValueFromCoord(id.x + 1, id.y    , id.z + 1);
//         //     cubeCorners[6] = edgeValueFromCoord(id.x    , id.y + 1, id.z + 1);
//         //     cubeCorners[7] = edgeValueFromCoord(id.x + 1, id.y + 1, id.z + 1);
//         // }

//         CubeToTris(cubeCorners, id);
//     }

//     void CubeToTris (byte[] cubeCorners, Vector3Int id) {
//         // Calculate unique index for each cube configuration.
//         // There are 256 possible values
//         // A value of 0 means cube is entirely inside surface; 255 entirely outside.
//         // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.

//         // int cubeIndex = 0;
//         // if (cubeCorners[0] > 127) cubeIndex |= 1;
//         // if (cubeCorners[1] > 127) cubeIndex |= 2; 
//         // if (cubeCorners[5] > 127) cubeIndex |= 4; 
//         // if (cubeCorners[4] > 127) cubeIndex |= 8;
//         // if (cubeCorners[2] > 127) cubeIndex |= 16;
//         // if (cubeCorners[3] > 127) cubeIndex |= 32; 
//         // if (cubeCorners[7] > 127) cubeIndex |= 64; 
//         // if (cubeCorners[6] > 127) cubeIndex |= 128;
//         int cubeIndex = (cubeCorners[0] & 128) >> 7 | (cubeCorners[1] & 128) >> 6 | (cubeCorners[5] & 128) >> 5 | (cubeCorners[4] & 128) >> 4 | 
//                         (cubeCorners[2] & 128) >> 3 | (cubeCorners[3] & 128) >> 2 | (cubeCorners[7] & 128) >> 1 | (cubeCorners[6] & 128);


        
//         // // Create triangles for current cube configuration
//         // for (int i = 0; triangulation[cubeIndex][i] != -1; i +=3) {
//         //     float3 val[3];
//         //     // Get indices of corner points A and B for each of the three edges
//         //     // of the cube that need to be joined to form the triangle.
//         //     for (int j = 0; j < 3; j++)
//         //     {
//         //         int edge = triangulation[cubeIndex][i+j];
//         //         int c1 = cornerIndexAFromEdge[edge]; 
//         //         int c2;                     
//         //         float3 vert = {0,0,0};            
//         //         uint3 d = {0,0,0};
//         //         int axis = 0;

//         //         // if ((edge == 1) || (edge == 5) || (edge == 9)  || (edge == 10)) vert.x = 1;
//         //         // if ((edge == 4) || (edge == 5) || (edge == 6)  || (edge == 7)) vert.y = 1;
//         //         // if ((edge == 2) || (edge == 6) || (edge == 10) || (edge == 11)) vert.z = 1;
//         //         vert.x = (edge == 1) | (edge == 5) | (edge == 9)  | (edge == 10);
//         //         vert.y = (edge == 4) | (edge == 5) | (edge == 6)  | (edge == 7);
//         //         vert.z = (edge == 2) | (edge == 6) | (edge == 10) | (edge == 11);
                
//         //         // if ((edge == 0) || (edge == 2) || (edge == 4)  || (edge == 6)) { d.x = 1; axis = 1; }
//         //         // if ((edge == 8) || (edge == 9) || (edge == 10) || (edge == 11)) { d.y = 1; axis = 2; }
//         //         // if ((edge == 1) || (edge == 3) || (edge == 5)  || (edge == 7)) { d.z = 1; axis = 4; }
//         //         d.y = (edge & 8) >> 3; axis |= d.y << 1;
//         //         d.z = edge & 1 & !d.y; axis |= d.z << 2;
//         //         d.x = !d.y & !d.z; axis |= d.x;

                
//         //         if (cubeCorners[c1] <= 127) {
//         //             c2 = c1;
//         //             c1 = cornerIndexBFromEdge[edge];
//         //         }
//         //         else c2 = cornerIndexBFromEdge[edge];

            
//         //         if (cubeCorners[c1] < 255 && cubeCorners[c2] > 0)
//         //         {
//         //             if ((c1 & axis) > 0) 
//         //             {
//         //                 vert += d * (1.0 - ((float)cubeCorners[c1]-128)/(cubeCorners[c1] - cubeCorners[c2]));
//         //             }
//         //             else 
//         //             {
//         //                 vert += d * ((float)cubeCorners[c1]-128)/(cubeCorners[c1] - cubeCorners[c2]);
//         //             }
//         //         }
//         //         else
//         //         {
//         //             if ((c1 & axis) > 0) 
//         //             {
//         //                 vert += d * ((1.0 - (cubeCorners[c1] + cubeCorners[c2] - 128)/255.0));
//         //             }
//         //             else 
//         //             {
//         //                 vert += d * ((cubeCorners[c1] + cubeCorners[c2] - 128)/255.0);
//         //             }  
//         //         }

//         //         val[j] = Origin + (float3)index + vert;
//         //     }

//         //     Triangle tri;
//         //     tri.vertexA = val[2];
//         //     tri.vertexB = val[1];
//         //     tri.vertexC = val[0];

//         //     triangles.Append(tri);
//         // }

//     }
// }
