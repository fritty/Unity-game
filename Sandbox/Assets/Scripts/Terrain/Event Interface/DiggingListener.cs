using System.Collections.Generic;
using UnityEngine;

/* Listens to Digging/Building events and sends modification requests */

public class DiggingListener : MonoBehaviour
{   
    private Dictionary<Vector3Int, Chunk> existingChunks; 

    private void Start()
    {
        existingChunks = ProTerra.Instance.existingChunks;
        GameEvents.Events.modifySingleBlock += ModifySingleBlock;
        GameEvents.Events.modifyClosestExposedBlock += ModifyClosestExposedBlock;

        //GameEvents.Events.modifyClosestBlock += ModifyClosestBlock;
        //GameEvents.Events.modifyBlockOnHit += ModifyBlockOnHit;
    } 

    // try to modify block on a given position
    private void ModifySingleBlock (Vector3Int blockPosition, int value)
    {   
        ProTerra.Instance.ModifyBlock(blockPosition, value); 
    }

    // modify closest to hitInfo.point visible block
    private void ModifyClosestExposedBlock(RaycastHit hitInfo, int value)
    {
        Vector3 position = hitInfo.point;
        Vector3Int cubePosition = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z));
        float minDistance = 10;
        Vector3Int targetBlock = Vector3Int.zero;

        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            Vector3Int blockPosition = new Vector3Int(cubePosition.x + (cornerIndex & 1), cubePosition.y + ((cornerIndex & 2) >> 1), cubePosition.z + ((cornerIndex & 4) >> 2));
            Vector3Int chunkCoord = ProTerra.WorldPositionToChunkCoord(blockPosition);
            Vector3Int localBlockPosition = ProTerra.WorldPositionToChunkPosition(blockPosition);

            byte blockValue = ProTerra.Instance.BlockValue(localBlockPosition, chunkCoord);

            if (blockValue > 0)
            {
                for (int directionIndex = 0; directionIndex < 3; directionIndex++)
                {
                    Vector3Int direction = new Vector3Int(directionIndex == 0 ? (blockPosition.x == cubePosition.x ? 1 : -1) : 0,
                                                          directionIndex == 1 ? (blockPosition.y == cubePosition.y ? 1 : -1) : 0,
                                                          directionIndex == 2 ? (blockPosition.z == cubePosition.z ? 1 : -1) : 0);
                    if (ProTerra.Instance.BlockValue(blockPosition + direction) == 0)
                    {
                        Vector3Int target = blockPosition;
                        float maxVertexValue = blockValue; 

                        Vector3Int offsetDirection = Vector3Int.zero;

                        if (directionIndex == 0) offsetDirection.y = 1;
                        else offsetDirection.x = 1;

                        byte offset_P = ProTerra.Instance.BlockValue(blockPosition + direction + offsetDirection);
                        byte offset_N = ProTerra.Instance.BlockValue(blockPosition + direction - offsetDirection);
                        byte offset_2P = ProTerra.Instance.BlockValue(blockPosition + direction + offsetDirection * 2);
                        byte offset_2N = ProTerra.Instance.BlockValue(blockPosition + direction - offsetDirection * 2);

                        if (InclineCondition.Check(offset_N, offset_P, offset_2N, offset_2P))
                        {
                            float vertexValue = InclineCondition.Evaluate(offset_N, offset_P, blockValue);
                            if (vertexValue > maxVertexValue)
                            {
                                maxVertexValue = vertexValue;
                                target = blockPosition + direction + (offset_N > 0 ? -offsetDirection : offsetDirection);
                            }
                        }

                        offsetDirection = Vector3Int.zero;

                        if (directionIndex == 2) offsetDirection.y = 1;
                        else offsetDirection.z = 1;

                        offset_P = ProTerra.Instance.BlockValue(blockPosition + direction + offsetDirection);
                        offset_N = ProTerra.Instance.BlockValue(blockPosition + direction - offsetDirection);
                        offset_2P = ProTerra.Instance.BlockValue(blockPosition + direction + offsetDirection * 2);
                        offset_2N = ProTerra.Instance.BlockValue(blockPosition + direction - offsetDirection * 2);

                        if (InclineCondition.Check(offset_N, offset_P, offset_2N, offset_2P))
                        {
                            float vertexValue = InclineCondition.Evaluate(offset_N, offset_P, blockValue);
                            if (vertexValue > maxVertexValue)
                            {
                                maxVertexValue = vertexValue;
                                target = blockPosition + direction + (offset_N > 0 ? -offsetDirection : offsetDirection);
                            }
                        } 

                        float distance = (target - position).magnitude;
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            targetBlock = target;
                        }
                    }
                }
            }
        }

        if (minDistance < 10)
            ProTerra.Instance.ModifyBlock(targetBlock, value);
        //else
        //    Debug.Log("block not found, @" + hitInfo.point);
    }


    //// try to modify block that is closest to given position 
    //private void ModifyClosestBlock(Vector3 position, int value)
    //{           
    //    Chunk chunk;

    //    float minDistance = 2;
    //    int minIndex = 8;
    //    Vector3Int cubePosition = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z)); 

    //    // go through cube corners and find closest one
    //    for (int i = 0; i < 8; i++)
    //    {
    //        Vector3Int blockPosition = new Vector3Int(cubePosition.x + (i & 1), cubePosition.y + ((i & 2) >> 1), cubePosition.z + ((i & 4) >> 2));
    //        Vector3Int chunkCoord = ProTerra.WorldPositionToChunkCoord(blockPosition);
    //        Vector3Int localBlockPosition = ProTerra.WorldPositionToChunkPosition(blockPosition);

    //        if (existingChunks.TryGetValue(chunkCoord, out chunk))
    //        {  
    //            if (chunk.blocks[localBlockPosition.z, localBlockPosition.y, localBlockPosition.x] > 0)
    //            {
    //                if (IsExposed(cubePosition, blockPosition))
    //                {
    //                    float distance = (position - blockPosition).magnitude;
    //                    if (distance < minDistance)
    //                    {
    //                        minDistance = distance;
    //                        minIndex = i;
    //                    }
    //                }
    //            }
    //        }
    //    }

    //    if (minIndex < 8)
    //    {
    //        Vector3Int blockPosition = new Vector3Int(cubePosition.x + (minIndex & 1), cubePosition.y + ((minIndex & 2) >> 1), cubePosition.z + ((minIndex & 4) >> 2));
    //        Vector3Int chunkCoord = ProTerra.WorldPositionToChunkCoord(blockPosition);
    //        Vector3Int localBlockPosition = ProTerra.WorldPositionToChunkPosition(blockPosition);

    //        ProTerra.Instance.ModifyBlock(chunkCoord, localBlockPosition, value);
    //    }
    //    else
    //        Debug.Log("No exposed blocks");
    //} 

    //private bool IsExposed (Vector3Int cubePosition, Vector3Int blockPosition)
    //{
    //    for (int i = 0; i < 3; i ++)
    //    {
    //        byte block = ProTerra.Instance.BlockValue(blockPosition);
    //        Vector3Int direction = new Vector3Int( i == 0 ? (blockPosition.x == cubePosition.x ? 1 : -1) : 0,
    //                                               i == 1 ? (blockPosition.y == cubePosition.y ? 1 : -1) : 0,
    //                                               i == 2 ? (blockPosition.z == cubePosition.z ? 1 : -1) : 0 );
            
    //        if (ProTerra.Instance.BlockValue(blockPosition + direction) == 0)
    //        {
    //            Vector3Int offsetAxis1 = new Vector3Int(0, 0, 0);
    //            Vector3Int offsetAxis2 = new Vector3Int(0, 0, 0);
    //            if (i == 0) { offsetAxis1.y = 1; offsetAxis2.z = 1; } else
    //            if (i == 1) { offsetAxis1.x = 1; offsetAxis2.z = 1; }
    //            else        { offsetAxis1.x = 1; offsetAxis2.y = 1; }

    //            byte offsetPositive = ProTerra.Instance.BlockValue(blockPosition + direction + offsetAxis1);
    //            byte offsetNegative = ProTerra.Instance.BlockValue(blockPosition + direction - offsetAxis1);

    //            if (!((offsetPositive > 0 && ProTerra.Instance.BlockValue(blockPosition + direction - offsetAxis1 * 2) == 0)) ^
    //                 ((offsetNegative > 0 && ProTerra.Instance.BlockValue(blockPosition + direction + offsetAxis1 * 2) == 0)))
    //                return true;
    //            else if ((255f * block / (255f - offsetNegative + block) < block) && (255f * block / (255f - offsetPositive + block) < block))
    //                return true;

    //            offsetPositive = ProTerra.Instance.BlockValue(blockPosition + direction + offsetAxis2);
    //            offsetNegative = ProTerra.Instance.BlockValue(blockPosition + direction - offsetAxis2);

    //            if (!((offsetPositive > 0 && ProTerra.Instance.BlockValue(blockPosition + direction - offsetAxis2 * 2) == 0)) ^
    //                 ((offsetNegative > 0 && ProTerra.Instance.BlockValue(blockPosition + direction + offsetAxis2 * 2) == 0)))
    //                    return true;
    //            else if ((255f * block / (255f - offsetNegative + block) < block) && (255f * block / (255f - offsetPositive + block) < block))
    //                return true;   
    //        }
    //    }

    //    return false;
    //}

    //// modify closest block that responsible for a vertex provided by hitInfo 
    //private void ModifyBlockOnHit (RaycastHit hitInfo, int value)
    //{
    //    Vector3 position = hitInfo.point;
    //    MeshCollider meshCollider = (MeshCollider)hitInfo.collider;
    //    if (meshCollider == null || meshCollider.sharedMesh == null)
    //        return;
    //    Mesh mesh = meshCollider.sharedMesh;
    //    Vector3[] vertices = mesh.vertices;
    //    int[] triangles = mesh.triangles;

    //    Vector3Int resultBlockPosition = Vector3Int.zero;
    //    Vector3Int resultChunkCoord = Vector3Int.zero;
    //    float minDistance = 2;

    //    Vector3Int chunkCoord;
    //    Vector3Int localBlockPosition;

    //    for (int i = 0; i < 3; i++)
    //    {
    //        Chunk chunk;
    //        Vector3 vertex = vertices[triangles[hitInfo.triangleIndex * 3 + i]];
    //        Vector3Int chunkOrigin = ProTerra.ChunkOriginFromCoord(ProTerra.WorldPositionToChunkCoord(position));            

    //        localBlockPosition = new Vector3Int((int)(vertex.x), (int)(vertex.y), (int)(vertex.z));            

    //        chunkCoord = ProTerra.WorldPositionToChunkCoord(localBlockPosition + chunkOrigin);

    //        if (localBlockPosition.x == Chunk.size.width) { localBlockPosition.x = 0; chunkCoord.x += 1; }
    //        if (localBlockPosition.y == Chunk.size.height) { localBlockPosition.y = 0; chunkCoord.y += 1; }
    //        if (localBlockPosition.z == Chunk.size.width) { localBlockPosition.z = 0; chunkCoord.z += 1; }

    //        if (existingChunks.TryGetValue(chunkCoord, out chunk))
    //        {
    //            if (chunk.blocks[localBlockPosition.z, localBlockPosition.y, localBlockPosition.x] == 0)
    //            {
    //                if (vertex.x != (int)vertex.x)
    //                    localBlockPosition.x += 1;
    //                if (vertex.y != (int)vertex.y)
    //                    localBlockPosition.y += 1;
    //                if (vertex.z != (int)vertex.z)
    //                    localBlockPosition.z += 1;
    //                if (localBlockPosition.x == Chunk.size.width) { localBlockPosition.x = 0; chunkCoord.x += 1; }
    //                if (localBlockPosition.y == Chunk.size.height) { localBlockPosition.y = 0; chunkCoord.y += 1; }
    //                if (localBlockPosition.z == Chunk.size.width) { localBlockPosition.z = 0; chunkCoord.z += 1; }
    //            }
    //            chunkOrigin = ProTerra.ChunkOriginFromCoord(chunkCoord);
    //            float distance = (position - (localBlockPosition + chunkOrigin)).magnitude;
    //            if (distance < minDistance)
    //            {
    //                minDistance = distance;
    //                resultBlockPosition = localBlockPosition;
    //                resultChunkCoord = ProTerra.WorldPositionToChunkCoord(localBlockPosition + chunkOrigin);
    //            }
    //        } 
    //    }  

    //    ProTerra.Instance.ModifyBlock(resultChunkCoord, resultBlockPosition, value);
    //} 

    private void OnDestroy()
    {
        GameEvents.Events.modifySingleBlock -= ModifySingleBlock;
        GameEvents.Events.modifyClosestExposedBlock -= ModifyClosestExposedBlock;

        //GameEvents.Events.modifyClosestBlock -= ModifyClosestBlock;
        //GameEvents.Events.modifyBlockOnHit -= ModifyBlockOnHit;
    }
}
