using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 dir = cam.transform.position - transform.position;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
