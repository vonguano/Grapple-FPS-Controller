using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;

    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
}
