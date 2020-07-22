using UnityEngine;
using System;

public class GameEvents : MonoBehaviour
{
    public static GameEvents Events; 

    private void Awake()
    {
        if (Events == null)
        {                                                 
            Events = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public event Action<Vector3Int, int> modifySingleBlock;
    public event Action<RaycastHit, int> modifyClosestExposedBlock; 

    public void ModifySingleBlock(Vector3Int position, int value)
    {
        modifySingleBlock?.Invoke(position, value);
    }

    public void ModifyClosestExposedBlock (RaycastHit hitInfo, int value)
    {
        modifyClosestExposedBlock?.Invoke(hitInfo, value);
    }
}
