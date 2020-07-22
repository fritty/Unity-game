using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    /* Listens to Digging/Building events and sends modification requests */

    public class DiggingListener : MonoBehaviour
    {
        ProTerra _terrain;

        private void Start()
        {
            _terrain = ProTerra.Instance;

            GameEvents.Events.modifySingleBlock += ModifySingleBlock;
            GameEvents.Events.modifyClosestExposedBlock += ModifyClosestExposedBlock;
        }

        // try to modify block on a given position
        private void ModifySingleBlock(Vector3Int blockPosition, int value)
        {
            _terrain.ModifyBlock(blockPosition, value);
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
                Vector3Int chunkCoord = blockPosition.ToChunkCoord();// ProTerra.WorldPositionToChunkCoord(blockPosition);
                Vector3Int localBlockPosition = blockPosition.ToChunkPosition();//ProTerra.WorldPositionToChunkPosition(blockPosition);

                byte blockValue = _terrain.GetBlockValue(localBlockPosition, chunkCoord);

                if (blockValue > 0)
                {
                    for (int directionIndex = 0; directionIndex < 3; directionIndex++)
                    {
                        Vector3Int direction = new Vector3Int(directionIndex == 0 ? (blockPosition.x == cubePosition.x ? 1 : -1) : 0,
                                                              directionIndex == 1 ? (blockPosition.y == cubePosition.y ? 1 : -1) : 0,
                                                              directionIndex == 2 ? (blockPosition.z == cubePosition.z ? 1 : -1) : 0);
                        if (_terrain.GetBlockValue(blockPosition + direction) == 0)
                        {
                            Vector3Int target = blockPosition;
                            float maxVertexValue = blockValue;

                            Vector3Int offsetDirection = Vector3Int.zero;

                            if (directionIndex == 0) offsetDirection.y = 1;
                            else offsetDirection.x = 1;

                            byte offset_P = _terrain.GetBlockValue(blockPosition + direction + offsetDirection);
                            byte offset_N = _terrain.GetBlockValue(blockPosition + direction - offsetDirection);
                            byte offset_2P = _terrain.GetBlockValue(blockPosition + direction + offsetDirection * 2);
                            byte offset_2N = _terrain.GetBlockValue(blockPosition + direction - offsetDirection * 2);

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

                            offset_P = _terrain.GetBlockValue(blockPosition + direction + offsetDirection);
                            offset_N = _terrain.GetBlockValue(blockPosition + direction - offsetDirection);
                            offset_2P = _terrain.GetBlockValue(blockPosition + direction + offsetDirection * 2);
                            offset_2N = _terrain.GetBlockValue(blockPosition + direction - offsetDirection * 2);

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
                _terrain.ModifyBlock(targetBlock, value);
        }

        private void OnDestroy()
        {
            GameEvents.Events.modifySingleBlock -= ModifySingleBlock;
            GameEvents.Events.modifyClosestExposedBlock -= ModifyClosestExposedBlock;
        }
    }
}