using UnityEngine;

namespace LPCG
{
    public class FishSwim : MonoBehaviour
    {
        public float speed = 3f;             // Fish speed
        public float lifetime = 120f;        // Time before resetting position
        public bool detectCollision = true;  // Determines if the fish should turn on collision instead of respawning

        public float detectionDistance = 2f; // Distance to detect edges
        public float frequency = 10f;        // Frequency of lateral movement
        public float amplitude = 0.32f;      // Intensity of lateral movement

        private Vector3 startPosition;       // Initial position of the fish
        private float timer;                 // Time counter

        void Start()
        {
            startPosition = transform.position;
            timer = 0f;
        }

        void Update()
        {
            // Move forward
            transform.Translate(Vector3.forward * speed * Time.deltaTime);

            // Oscillating lateral movement (zigzag motion)
            float sideMovement = Mathf.Sin(Time.time * frequency) * amplitude;

            transform.Rotate(Vector3.up * sideMovement);

            // If bump detection is enabled, use Raycast to detect edges
            if (detectCollision)
            {
                DetectCollision();
            }
            else
            {
                // If bump detection is disabled, respawn occurs after the lifetime expires
                timer += Time.deltaTime;
                if (timer >= lifetime)
                {
                    Respawn();
                }
            }
        }

        void DetectCollision()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, detectionDistance))
                TurnFish();
        }

        void TurnFish()
        {
            transform.Rotate(Vector3.up * Random.Range(23, 90)); ;
        }

        void Respawn()
        {
            // Reset the fish position only if bump detection is disabled
            transform.position = startPosition;
            timer = 0f;
        }
    }
}