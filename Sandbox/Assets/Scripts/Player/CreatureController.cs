using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CreatureController : MonoBehaviour
{
    [SerializeField]    
    [Range(1, 50)]
    float speed = 10;
    [SerializeField]
    [Range(1, 100)]
    float rotationSpeed = 50; // dergees per second
    [SerializeField]
    Vector3 startingPosition = Vector3.up * 50; 
    
    public Vector3 position { get; private set; }
    public Vector3 lookDirection { get; private set; }

    private ICreatureInput input;
    private Rigidbody body; 

    private void Awake()
    {
        input = GetComponent<ICreatureInput>();
        body = GetComponent<Rigidbody>();
        lookDirection = Vector3.forward;
        transform.position = startingPosition; 
        if (body != null)
        {   
            body.rotation = Quaternion.LookRotation(lookDirection);
            body.freezeRotation = true;
        }
    }   

    private void OnValidate()
    {
        if (input == null)
            input = GetComponent<ICreatureInput>();
        if (body == null)
            body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        position = transform.position;
        if (input != null && body != null)
            Move();
    }

    private void Move ()
    {
        if (input.Updated)
        {
            Vector3 velocity = body.velocity;
            Vector3 inputVelocity = (new Vector3(input.DirectionX, 0, input.DirectionZ)).normalized * speed;
            inputVelocity.y = input.Jump ? speed : 0;

            Vector2 inputRotation = new Vector2(input.RotationHorizontal, input.RotationVertical) * rotationSpeed; // in degrees
            float horizontalRotationAngle, verticalRotationAngle; // in radians
            Vector3 newLookDirection = lookDirection;

            transform.Rotate(0, inputRotation.x * Time.fixedDeltaTime, 0); 

            horizontalRotationAngle = transform.rotation.eulerAngles.y * Mathf.Deg2Rad; 
            verticalRotationAngle = Mathf.Asin(lookDirection.y);

            verticalRotationAngle = Mathf.Clamp(verticalRotationAngle + inputRotation.y * Mathf.Deg2Rad  * Time.fixedDeltaTime, -90 * Mathf.Deg2Rad, 90 * Mathf.Deg2Rad);

            newLookDirection.y = Mathf.Sin(verticalRotationAngle);
            newLookDirection.x = Mathf.Cos(verticalRotationAngle) * Mathf.Sin(horizontalRotationAngle);
            newLookDirection.z = Mathf.Cos(verticalRotationAngle) * Mathf.Cos(horizontalRotationAngle); 

            inputVelocity = transform.TransformVector(inputVelocity);

            velocity.x = Mathf.Clamp(velocity.x + inputVelocity.x * Time.fixedDeltaTime, -speed, speed);
            velocity.y = Mathf.Clamp(velocity.y + inputVelocity.y, -speed, speed);
            velocity.z = Mathf.Clamp(velocity.z + inputVelocity.z * Time.fixedDeltaTime, -speed, speed);

            body.velocity = velocity;
            lookDirection = newLookDirection.normalized;
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(body.position, body.position + lookDirection);
        }
    }
}
