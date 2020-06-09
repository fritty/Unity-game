using UnityEngine;

[RequireComponent(typeof(CreatureController), typeof(PlayerInput))]
public class Player : MonoBehaviour
{
    [SerializeField]
    bool showPosition = false;
    public Camera playerCamera; 

    CreatureController controller;

    private void Awake()
    {
        controller = GetComponent<CreatureController>();

        if (playerCamera == null)
            playerCamera = Camera.main;  
        
        playerCamera.transform.SetParent(transform);
        playerCamera.transform.position = transform.position;        
    } 

    private void Update()
    {            
        playerCamera.transform.LookAt(playerCamera.transform.position + controller.lookDirection);
    }

    private void OnDrawGizmos()
    {
        if (showPosition)
        {
            float minX = Mathf.Floor(transform.position.x);
            float minZ = Mathf.Floor(transform.position.z);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector3(minX, 0, minZ), new Vector3(minX, 32, minZ));
            Gizmos.DrawLine(new Vector3(minX, 0, minZ + 1), new Vector3(minX, 32, minZ + 1));
            Gizmos.DrawLine(new Vector3(minX + 1, 0, minZ), new Vector3(minX + 1, 32, minZ));
            Gizmos.DrawLine(new Vector3(minX + 1, 0, minZ + 1), new Vector3(minX + 1, 32, minZ + 1));
        }
    }
}