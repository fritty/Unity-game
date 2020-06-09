using System.Collections;
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
        GameEvents.Events.modifyClosestBlock += ModifyClosestBlock;
        GameEvents.Events.modifyBlockOnHit += ModifyBlockOnHit;
    }

   

    // try to modify block on a given position
    private void ModifySingleBlock (Vector3Int position, int value)
    {

        Vector3Int chunkCoord = ProTerra.WorldPositionToChunkCoord(position);
        if (existingChunks.ContainsKey(chunkCoord))
        {
            Vector3Int localBlockPosition = ProTerra.WorldPositionToChunkPosition(position);

            ProTerra.Instance.ModifyBlock(chunkCoord, localBlockPosition, value);
        }
    }

    // try to modify block that is closest to given position 
    private void ModifyClosestBlock(Vector3 position, int value)
    {           
        Chunk chunk;

        float minDistance = 2;
        int minIndex = 8;
        Vector3Int cubePosition = new Vector3Int((int)position.x, (int)position.y, (int)position.z);  

        // go through cube corners and find closest one
        for (int i = 0; i < 8; i++)
        {
            Vector3Int blockPosition = new Vector3Int(cubePosition.x + (i & 1), cubePosition.y + ((i & 2) >> 1), cubePosition.z + ((i & 4) >> 2));
            Vector3Int chunkCoord = ProTerra.WorldPositionToChunkCoord(blockPosition);
            Vector3Int localBlockPosition = ProTerra.WorldPositionToChunkPosition(blockPosition);

            if (existingChunks.TryGetValue(chunkCoord, out chunk))
            {  
                if (chunk.blocks[localBlockPosition.z, localBlockPosition.y, localBlockPosition.x] > 0)
                {
                    float distance = (position - blockPosition).magnitude;
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minIndex = i;
                    }
                }
            }
        }

        if (minIndex < 8)
        {
            Vector3Int blockPosition = new Vector3Int(cubePosition.x + (minIndex & 1), cubePosition.y + ((minIndex & 2) >> 1), cubePosition.z + ((minIndex & 4) >> 2));
            Vector3Int chunkCoord = ProTerra.WorldPositionToChunkCoord(blockPosition);
            Vector3Int localBlockPosition = ProTerra.WorldPositionToChunkPosition(blockPosition); 

            ProTerra.Instance.ModifyBlock(chunkCoord, localBlockPosition, value);            
        }
    }

    // modify closest block that responsible for a vertex provided by hitInfo 
    private void ModifyBlockOnHit (RaycastHit hitInfo, int value)
    {
        Vector3 position = hitInfo.point;
        MeshCollider meshCollider = (MeshCollider)hitInfo.collider;
        if (meshCollider == null || meshCollider.sharedMesh == null)
            return;
        Mesh mesh = meshCollider.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Vector3Int resultBlockPosition = Vector3Int.zero;
        Vector3Int resultChunkCoord = Vector3Int.zero;
        float minDistance = 2;

        Vector3Int chunkCoord;
        Vector3Int localBlockPosition;

        for (int i = 0; i < 3; i++)
        {
            Chunk chunk;
            Vector3 vertex = vertices[triangles[hitInfo.triangleIndex * 3 + i]];
            Vector3Int chunkOrigin = ProTerra.ChunkOriginFromCoord(ProTerra.WorldPositionToChunkCoord(position));            

            localBlockPosition = new Vector3Int((int)(vertex.x), (int)(vertex.y), (int)(vertex.z));            

            chunkCoord = ProTerra.WorldPositionToChunkCoord(localBlockPosition + chunkOrigin);

            if (localBlockPosition.x == Chunk.size.width) { localBlockPosition.x = 0; chunkCoord.x += 1; }
            if (localBlockPosition.y == Chunk.size.height) { localBlockPosition.y = 0; chunkCoord.y += 1; }
            if (localBlockPosition.z == Chunk.size.width) { localBlockPosition.z = 0; chunkCoord.z += 1; }

            if (existingChunks.TryGetValue(chunkCoord, out chunk))
            {
                if (chunk.blocks[localBlockPosition.z, localBlockPosition.y, localBlockPosition.x] == 0)
                {
                    if (vertex.x != (int)vertex.x)
                        localBlockPosition.x += 1;
                    if (vertex.y != (int)vertex.y)
                        localBlockPosition.y += 1;
                    if (vertex.z != (int)vertex.z)
                        localBlockPosition.z += 1;
                    if (localBlockPosition.x == Chunk.size.width) { localBlockPosition.x = 0; chunkCoord.x += 1; }
                    if (localBlockPosition.y == Chunk.size.height) { localBlockPosition.y = 0; chunkCoord.y += 1; }
                    if (localBlockPosition.z == Chunk.size.width) { localBlockPosition.z = 0; chunkCoord.z += 1; }
                }
                chunkOrigin = ProTerra.ChunkOriginFromCoord(chunkCoord);
                float distance = (position - (localBlockPosition + chunkOrigin)).magnitude;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    resultBlockPosition = localBlockPosition;
                    resultChunkCoord = ProTerra.WorldPositionToChunkCoord(localBlockPosition + chunkOrigin);
                }
            } 
        }  

        ProTerra.Instance.ModifyBlock(resultChunkCoord, resultBlockPosition, value);
    } 

    private void OnDestroy()
    {
        GameEvents.Events.modifySingleBlock -= ModifySingleBlock;
        GameEvents.Events.modifyClosestBlock -= ModifyClosestBlock;
        GameEvents.Events.modifyBlockOnHit -= ModifyBlockOnHit;
    }
}
