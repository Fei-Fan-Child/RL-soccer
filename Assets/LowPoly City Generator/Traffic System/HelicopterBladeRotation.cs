using UnityEngine;

namespace LPCG
{
    public class HelicopterBladeRotation : MonoBehaviour
    {
        public float rotationSpeed = 1000f; // Velocidade da rotação

        void Update()
        {
            transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}