using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    public interface IHeightMapFunction
    {
        float Evaluate(Vector2Int position);
    }
}


