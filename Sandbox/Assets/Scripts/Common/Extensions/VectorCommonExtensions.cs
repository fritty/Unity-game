using UnityEngine;

public static class VectorCommonExtensions
{
    public static Vector2 XZ(this Vector3 vector) => new Vector2(vector.x, vector.z);          
    public static Vector3 X0Y(this Vector2 vector) => new Vector3(vector.x, 0, vector.y);      
    public static Vector3 VecX(this Vector3 vector) => new Vector3(vector.x, 0, 0);
    public static Vector3 VecY(this Vector3 vector) => new Vector3(0, vector.y, 0);
    public static Vector3 VecZ(this Vector3 vector) => new Vector3(0, 0, vector.z); 
    public static Vector2Int XZ(this Vector3Int vector) => new Vector2Int(vector.x, vector.z); 
    public static Vector3Int X0Y(this Vector2Int vector) => new Vector3Int(vector.x, 0, vector.y);
    public static Vector3Int VecX(this Vector3Int vector) => new Vector3Int(vector.x, 0, 0);
    public static Vector3Int VecY(this Vector3Int vector) => new Vector3Int(0, vector.y, 0);
    public static Vector3Int VecZ(this Vector3Int vector) => new Vector3Int(0, 0, vector.z);

    /// <summary>
    /// Sum of base vector and given components
    /// </summary>
    public static Vector2 Plus(this Vector2 vector, float x, float y) => new Vector2(vector.x + x, vector.y + y);
    /// <summary>
    /// Sum of base vector and given components
    /// </summary>
    public static Vector2Int Plus(this Vector2Int vector, int x, int y) => new Vector2Int(vector.x + x, vector.y + y);
    /// <summary>
    /// Sum of base vector and given components
    /// </summary>
    public static Vector3 Plus(this Vector3 vector, float x, float y, float z) => new Vector3(vector.x + x, vector.y + y, vector.z + z);
    /// <summary>
    /// Sum of base vector and given components
    /// </summary>
    public static Vector3Int Plus(this Vector3Int vector, int x, int y, int z) => new Vector3Int(vector.x + x, vector.y + y, vector.z + z);
}
