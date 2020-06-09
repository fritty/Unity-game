using System.Collections;
using System.Collections.Generic;
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
    public event Action<Vector3, int> modifyClosestBlock;
    public event Action<RaycastHit, int> modifyBlockOnHit;

    public void ModifySingleBlock (Vector3Int position, int value)
    {
        modifySingleBlock?.Invoke(position, value);
    }

    public void ModifyClosestBlock(Vector3 position, int value)
    {
        modifyClosestBlock?.Invoke(position, value);
    }

    public void ModifyBlockOnHit (RaycastHit hitInfo, int value)
    {
        modifyBlockOnHit?.Invoke(hitInfo, value);
    }
}
