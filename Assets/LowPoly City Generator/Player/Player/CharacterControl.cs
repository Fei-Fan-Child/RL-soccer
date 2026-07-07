using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FCG
{
    public class CharacterControl : MonoBehaviour
    {
        public float speed = 10.0f;
        public float sensitivity = 100f;

        private float xRotation = 0f;
        private float yRotation = 0f;

        private Transform cam;
        private CharacterController charController;

        private Vector3 initialPosition = Vector3.zero;

        void Start()
        {

            Cursor.lockState = CursorLockMode.Locked;

            charController = GetComponent<CharacterController>();
            cam = transform.Find("Camera");

            cam.localRotation = Quaternion.identity;

            initialPosition = transform.position;

#if !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogWarning("⚠️ Input System is set to 'New' only. This script uses the old Input Manager. To fix this, go to Edit > Project Settings > Player > Active Input Handling and set it to 'Both'.");
#endif

        }

        void Update()
        {

            CameraMovement();
            MoveCharacter();

            if (transform.position.y < -10)
                transform.position = initialPosition;
        }

        void CameraMovement()
        {

#if ENABLE_LEGACY_INPUT_MANAGER

            float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity * Time.deltaTime;
            float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity * Time.deltaTime;

            yRotation += mouseX;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -70f, 70f);

            cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
#endif
        }

        void MoveCharacter()
        {
#if ENABLE_LEGACY_INPUT_MANAGER

            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 move = (transform.right * moveX) + (transform.forward * moveZ);
            move = Vector3.ClampMagnitude(move, 1.0f);

            float finalSpeed = Input.GetKey(KeyCode.LeftShift) ? speed * 1.2f : speed;
            charController.SimpleMove(move * finalSpeed);
#endif
        }
    }
}
