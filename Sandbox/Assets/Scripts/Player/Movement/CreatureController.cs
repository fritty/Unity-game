using System.Threading;
using UnityEngine;

// Directs input to movement classes and applies movement
[RequireComponent(typeof(CharacterController))]
public class CreatureController : MonoBehaviour
{
    [SerializeField]    
    [Range(1, 50)]
    float speed = 10;
    [SerializeField]
    [Range(1, 100)]
    float rotationSpeed = 50; // dergees per second       

    public Vector3 Position => transform.position;
    public Vector3 Velocity => _velocity;
    public Vector3 LookDirection { get; private set; }

    ICreatureInput _input;
    IJumpMove _jump;
    ISneakMove _sneak;
    CharacterController _controller;

    Vector3 _velocity;
    Vector3 _inputDirection;

    public void SetPosition (Vector3 position)
    {
        _controller.enabled = false;
        _controller.transform.position = position;
        _controller.enabled = true;
    }

    public void SetJumping (IJumpMove jump, bool active)
    {
        if (!active)
        {
            if (_jump == jump)
                _jump = null;
            return;
        }

        if (_jump != jump)
            _jump = jump;
    }

    public void SetSneaking(ISneakMove sneak, bool active)
    {
        if (!active)
        {
            if (_sneak == sneak)
                _sneak = null;
            return;
        }

        if (_sneak != sneak)
            _sneak = sneak;
    }

    private void Awake()
    {
        _input = GetComponent<ICreatureInput>();
        _controller = GetComponent<CharacterController>();
        LookDirection = Vector3.forward; 
        if (_controller != null)
        {   
            transform.rotation = Quaternion.LookRotation(LookDirection);
        }
    }   

    private void LateUpdate()
    {
        if (_input == null) return;

        _inputDirection = (new Vector3(_input.DirectionX, 0, _input.DirectionZ)).normalized;
        if (_jump != null)
            _jump.ApplyJumpInput(_input.Jump, _input.JumpContinuous);
        if (_sneak != null)
            _sneak.ApplySneakingInput(_input.Sneak, _input.SneakContinuous);
    }

    private void FixedUpdate()
    {
        if (_input == null) return;

        Rotate();
        Move();
    }

    private void Move ()
    {
        Vector3 movement = transform.TransformDirection(_inputDirection) * speed * Time.fixedDeltaTime;

        if (_jump != null)
            _velocity.y = _jump.Jump(_velocity.y, _controller.isGrounded);
        else
        {
            _velocity.y += Physics.gravity.y * Time.fixedDeltaTime;   // default fall
            if (_velocity.y < 0 && _controller.isGrounded)
                _velocity.y = 0;
        }

        _controller.Move(movement + _velocity.VecY() * Time.fixedDeltaTime);

        _velocity.x = _controller.velocity.x;
        _velocity.z = _controller.velocity.z;
    }

    private void Rotate ()
    {
        if (_input.Rotated)
        {
            Vector2 inputRotation = new Vector2(_input.RotationHorizontal, _input.RotationVertical) * rotationSpeed; // in degrees
            float horizontalRotationAngle, verticalRotationAngle; // in radians
            Vector3 newLookDirection = LookDirection;
            transform.Rotate(0, inputRotation.x * Time.fixedDeltaTime, 0);

            horizontalRotationAngle = transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
            verticalRotationAngle = Mathf.Asin(LookDirection.y);

            verticalRotationAngle = Mathf.Clamp(verticalRotationAngle + inputRotation.y * Mathf.Deg2Rad * Time.fixedDeltaTime, -90 * Mathf.Deg2Rad, 90 * Mathf.Deg2Rad);

            newLookDirection.y = Mathf.Sin(verticalRotationAngle);
            newLookDirection.x = Mathf.Cos(verticalRotationAngle) * Mathf.Sin(horizontalRotationAngle);
            newLookDirection.z = Mathf.Cos(verticalRotationAngle) * Mathf.Cos(horizontalRotationAngle);

            LookDirection = newLookDirection.normalized;
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + LookDirection);
        }
    }
}
