using UnityEngine;

public class ISOCameraMovement : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 offset = new Vector3(5f, 5f, -5f);
    [SerializeField] private float smoothSpeed = 0.125f;

    private Vector3 targetPosition;

    void Start()
    {
        if (player == null)
        {
            player = Object.FindFirstObjectByType<Transform>(); // Fallback: find player if not assigned
        }

        if (player != null)
        {
            targetPosition = player.position + offset;
            transform.position = targetPosition;
        }
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // Calculate the desired position based on player position and offset
        targetPosition = player.position + offset;

        // Smoothly move camera towards target position
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed);

        // Make camera look at player
        transform.LookAt(player.position);
    }
}
