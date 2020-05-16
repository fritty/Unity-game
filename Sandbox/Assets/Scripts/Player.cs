using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public GameObject cam;
    public float Speed;
    public float RotationSpeed;

    RaycastHit hit;
    List<Vector3> wat;

    void Start()
    {
        transform.position = Vector3.up * 15;
        transform.rotation = Quaternion.LookRotation(new Vector3(0,0,1));
        cam.transform.SetParent(transform);
        cam.transform.position = transform.position;
    }

    void Update()
    {       
        if (Input.anyKey) {
            Vector3 inputDirection = new Vector3(0,0,0);
            Vector3 inputRotation = new Vector3(0,0,0);

            inputDirection.x += Input.GetKey(KeyCode.D) ? Speed*Time.deltaTime : Input.GetKey(KeyCode.A) ? -Speed*Time.deltaTime : 0;
            inputDirection.z += Input.GetKey(KeyCode.W) ? Speed*Time.deltaTime : Input.GetKey(KeyCode.S) ? -Speed*Time.deltaTime : 0;
            inputDirection.y += Input.GetKey(KeyCode.Space) ? Speed*Time.deltaTime : Input.GetKey(KeyCode.LeftShift) ? -Speed*Time.deltaTime : 0;

            inputRotation.x += Input.GetKey(KeyCode.UpArrow) ? RotationSpeed*Time.deltaTime : Input.GetKey(KeyCode.DownArrow) ? -RotationSpeed*Time.deltaTime : 0f;
            inputRotation.y += Input.GetKey(KeyCode.RightArrow) ? RotationSpeed*Time.deltaTime : Input.GetKey(KeyCode.LeftArrow) ? -RotationSpeed*Time.deltaTime : 0f;

            transform.Translate(inputDirection, Space.Self);
            transform.Rotate(0, inputRotation.y, 0);
            cam.transform.Rotate(inputRotation.x, 0, 0);
        }
    }
}
