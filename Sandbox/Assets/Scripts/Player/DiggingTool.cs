using UnityEngine;

public class DiggingTool : MonoBehaviour
{
    [SerializeField]
    float maxDistance = 10;
    [SerializeField]
    int value = -10;

    [SerializeField]
    bool highlightGizmo = false;


    private CreatureController controller;
    private ICreatureInput input;
    
    private bool drawGizmo;
    private Vector3 rayGizmoStart;
    private Vector3 rayGizmoEnd;

    private void Awake()
    {
        controller = GetComponent<CreatureController>();
        input = GetComponent<ICreatureInput>();        
    }

    private void OnValidate()
    {
        if (input == null)
            input = GetComponent<ICreatureInput>();
        if (controller == null)
            controller = GetComponent<CreatureController>();        
    }

    private void FixedUpdate()
    {
        if (input != null && controller != null)
            UseTool();  
    } 

    private void UseTool ()
    {
        drawGizmo = false;
        if (input.UseContinuous)
        {
            Ray ray = new Ray(controller.position, controller.lookDirection);
            RaycastHit hitInfo;

            if (Physics.Raycast(ray, out hitInfo, maxDistance: maxDistance))
            {
                if (hitInfo.transform.tag == "Chunk")
                {    
                    GameEvents.Events.ModifyClosestExposedBlock(hitInfo, value);

                    rayGizmoStart = controller.position;
                    rayGizmoEnd = hitInfo.point;
                    drawGizmo = true;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {           
        if (Application.isPlaying && (drawGizmo || highlightGizmo))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(rayGizmoStart, rayGizmoEnd);

            Vector3Int intPosition = new Vector3Int(Mathf.FloorToInt(rayGizmoEnd.x), Mathf.FloorToInt(rayGizmoEnd.y), Mathf.FloorToInt(rayGizmoEnd.z));             

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(intPosition.x + .5f, intPosition.y + .5f, intPosition.z + .5f), Vector3.one);
        }
    }
}
