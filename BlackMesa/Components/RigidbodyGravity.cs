using UnityEngine;

public class RigidbodyGravity : MonoBehaviour
{
    public Vector3 gravity = Vector3.down * 15.9f;

    private Rigidbody rigidBody;

    private void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        rigidBody.useGravity = false;
    }

    private void FixedUpdate()
    {
        rigidBody.AddForce(gravity, ForceMode.Acceleration);
    }
}
