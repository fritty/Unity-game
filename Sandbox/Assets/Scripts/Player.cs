using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public GameObject cam;
    public float speed;
    public Vector3 position;
    Vector3 rotation;

    RaycastHit hit;
    List<Vector3> wat;

    // Start is called before the first frame update
    void Start()
    {
        position = Vector3.up * 15;
        rotation = Vector3.zero;
        transform.position = position;
        cam.transform.SetParent(transform);
        cam.transform.position = position;
    }

    // Update is called once per frame
    void Update()
    {        
        Vector3 inputDirection = new Vector3(0,0,0);//new Vector3 (Input.GetAxisRaw("Horisontal"), 0, Input.GetAxisRaw("Vertical"));
        inputDirection.z += Input.GetKey(KeyCode.W) ? speed*Time.deltaTime : Input.GetKey(KeyCode.S) ? -speed*Time.deltaTime : 0;
        inputDirection.x += Input.GetKey(KeyCode.D) ? speed*Time.deltaTime : Input.GetKey(KeyCode.A) ? -speed*Time.deltaTime : 0;

        position.Set(inputDirection.x, inputDirection.y, inputDirection.z);
        rotation.x += Input.mouseScrollDelta.x/10;
        rotation.y += Input.mouseScrollDelta.y/10;

        transform.Translate(position, Space.Self);
        transform.Rotate(rotation.x, rotation.y, 0);
    }
}
