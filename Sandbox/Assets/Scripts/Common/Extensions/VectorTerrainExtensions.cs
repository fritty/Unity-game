using UnityEngine;


public static class VectorTerrainExtensions
{
    public static Vector3Int ToChunkCoord(this Vector3Int position) => new Vector3Int(Mathf.FloorToInt((float)position.x / ChunkSize.Width), Mathf.FloorToInt((float)position.y / ChunkSize.Height), Mathf.FloorToInt((float)position.z / ChunkSize.Width));

    public static Vector3Int ToChunkCoord(this Vector3 position) => new Vector3Int(Mathf.FloorToInt(position.x / ChunkSize.Width), Mathf.FloorToInt(position.y / ChunkSize.Height), Mathf.FloorToInt(position.z / ChunkSize.Width));

    public static Vector2Int ToChunkCoord(this Vector2Int position) => new Vector2Int(Mathf.FloorToInt((float)position.x / ChunkSize.Width), Mathf.FloorToInt((float)position.y / ChunkSize.Width));

    public static Vector2Int ToChunkCoord(this Vector2 position) => new Vector2Int(Mathf.FloorToInt(position.x / ChunkSize.Width), Mathf.FloorToInt(position.y / ChunkSize.Width));

    public static Vector3Int ToChunkPosition(this Vector3Int position)
    {
        Vector3Int result = new Vector3Int(position.x % ChunkSize.Width, position.y % ChunkSize.Height, position.z % ChunkSize.Width);
        if (result.x < 0)
            result.x += ChunkSize.Width;
        if (result.y < 0)
            result.y += ChunkSize.Height;
        if (result.z < 0)
            result.z += ChunkSize.Width;

        return result;
    }

    public static Vector3Int ToChunkPosition(this Vector3 position)
    {
        Vector3Int result = new Vector3Int(Mathf.FloorToInt(position.x) % ChunkSize.Width, Mathf.FloorToInt(position.y) % ChunkSize.Height, Mathf.FloorToInt(position.z) % ChunkSize.Width);
        if (result.x < 0)
            result.x += ChunkSize.Width;
        if (result.y < 0)
            result.y += ChunkSize.Height;
        if (result.z < 0)
            result.z += ChunkSize.Width;

        return result;
    }

    public static Vector2Int ToChunkPosition(this Vector2 vector) => ToChunkPosition(vector.X0Y()).XZ();
    public static Vector2Int ToChunkPosition(this Vector2Int vector) => ToChunkPosition(vector.X0Y()).XZ();

    /// <summary>
    /// Chunk world position from its coordinate
    /// </summary>
    public static Vector3Int ToChunkOrigin(this Vector3Int chunkCoord) => new Vector3Int(chunkCoord.x * ChunkSize.Width, chunkCoord.y * ChunkSize.Height, chunkCoord.z * ChunkSize.Width);

    /// <summary>
    /// Chunk world position from its coordinate
    /// </summary>
    public static Vector2Int ToChunkOrigin(this Vector2Int chunkCoord) => new Vector2Int(chunkCoord.x * ChunkSize.Width, chunkCoord.y * ChunkSize.Width);
}
