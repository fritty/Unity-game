using UnityEngine;

[RequireComponent(typeof(CreatureController), typeof(PlayerInput))]
public class Player : MonoBehaviour
{
    [SerializeField]
    bool _showPosition_ = false;
    public Camera PlayerCamera; 

    CreatureController _controller;

    private void Awake()
    {
        _controller = GetComponent<CreatureController>();

        if (PlayerCamera == null)
            PlayerCamera = Camera.main;

        PlayerCamera.transform.SetParent(transform);
        PlayerCamera.transform.position = transform.position;        
    }

    private void Start()
    {
        _controller.SetPosition(new Vector3(64, 200, -20));
    }

    private void Update()
    {            
        PlayerCamera.transform.LookAt(PlayerCamera.transform.position + _controller.LookDirection);
    }

    private void OnDrawGizmos()
    {
        if (_showPosition_)
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