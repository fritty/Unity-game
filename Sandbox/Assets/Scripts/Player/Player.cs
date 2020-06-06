using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public Camera cam;
    public float Speed = 10;
    public float RotationSpeed = 50;    

    Rigidbody playerRigidbody;

    private void Start()
    {
        if (cam == null)
            cam = Camera.main;

        playerRigidbody = GetComponent<Rigidbody>();

        transform.position = Vector3.up * 60;
        transform.rotation = Quaternion.LookRotation(new Vector3(0,0,1));
        cam.transform.SetParent(transform);
        cam.transform.position = transform.position;
    }

    private void FixedUpdate()
    {   
        if (Input.anyKey)
        {
            Vector3 newVelocity = playerRigidbody.velocity;
            Vector3 inputDirection = new Vector3(0, 0, 0);
            Vector3 inputRotation = new Vector3(0, 0, 0);
            Vector3 velocity;

            inputDirection.x += Input.GetKey(KeyCode.D) ? 1 : Input.GetKey(KeyCode.A) ? -1 : 0;
            inputDirection.z += Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0;   

            velocity = inputDirection.normalized * Speed;

            inputRotation.x += Input.GetKey(KeyCode.UpArrow) ? RotationSpeed * Time.deltaTime : Input.GetKey(KeyCode.DownArrow) ? -RotationSpeed * Time.deltaTime : 0f;
            inputRotation.y += Input.GetKey(KeyCode.RightArrow) ? RotationSpeed * Time.deltaTime : Input.GetKey(KeyCode.LeftArrow) ? -RotationSpeed * Time.deltaTime : 0f;

            transform.Rotate(0, inputRotation.y, 0);
            cam.transform.Rotate(inputRotation.x, 0, 0);

            velocity = transform.TransformVector(velocity);
            velocity.y = (Input.GetKeyDown(KeyCode.Space) ? 1 : Input.GetKeyDown(KeyCode.LeftShift) ? -1 : 0) * Speed;

            newVelocity.x = Mathf.Clamp(newVelocity.x + velocity.x * Time.fixedDeltaTime, -Speed, Speed);
            newVelocity.y = Mathf.Clamp(newVelocity.y + velocity.y, -Speed, Speed);
            newVelocity.z = Mathf.Clamp(newVelocity.z + velocity.z * Time.fixedDeltaTime, -Speed, Speed);

            playerRigidbody.velocity = newVelocity;
        }       
    }

    private void OnDrawGizmos () {
        if (Application.isPlaying) {
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