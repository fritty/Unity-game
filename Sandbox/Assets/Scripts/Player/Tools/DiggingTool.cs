using UnityEngine;

public class DiggingTool : MonoBehaviour
{
    [SerializeField]
    float maxDistance = 10;
    [SerializeField]
    int value = -10;
    [SerializeField]
    bool diggingGizmo = true;


    CreatureController _controller;
    ICreatureInput _input;
    
    bool _drawGizmo;
    Vector3 _rayGizmoStart;
    Vector3 _rayGizmoEnd;

    private void Awake()
    {
        _controller = GetComponent<CreatureController>();
        _input = GetComponent<ICreatureInput>();        
    }

    private void OnValidate()
    {
        if (_input == null)
            _input = GetComponent<ICreatureInput>();
        if (_controller == null)
            _controller = GetComponent<CreatureController>();        
    }

    private void FixedUpdate()
    {
        if (_input != null && _controller != null)
            UseTool();  
    } 

    private void UseTool ()
    {           
        if (_input.UseContinuous)
        {
            Ray ray = new Ray(_controller.Position, _controller.LookDirection);
            RaycastHit hitInfo;

            if (Physics.Raycast(ray, out hitInfo, maxDistance: maxDistance))
            {
                if (hitInfo.transform.tag == "Chunk")
                {    
                    GameEvents.Events.ModifyClosestExposedBlock(hitInfo, value);

                    _rayGizmoStart = _controller.Position;
                    _rayGizmoEnd = hitInfo.point;
                    _drawGizmo = true;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {           
        if (Application.isPlaying && (_drawGizmo && diggingGizmo))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_rayGizmoStart, _rayGizmoEnd);

            Vector3Int intPosition = new Vector3Int(Mathf.FloorToInt(_rayGizmoEnd.x), Mathf.FloorToInt(_rayGizmoEnd.y), Mathf.FloorToInt(_rayGizmoEnd.z));             

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(intPosition.x + .5f, intPosition.y + .5f, intPosition.z + .5f), Vector3.one);

            _drawGizmo = false;
        }
    }
}
