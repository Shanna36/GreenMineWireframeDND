using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    void Update()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        float horizontal = 0f;
        float vertical = 0f;

        // WASD input
        if (Input.GetKey(KeyCode.W))
            vertical += 1f;
        if (Input.GetKey(KeyCode.S))
            vertical -= 1f;
        if (Input.GetKey(KeyCode.A))
            horizontal -= 1f;
        if (Input.GetKey(KeyCode.D))
            horizontal += 1f;

        // Arrow key input
        horizontal += Input.GetAxis("Horizontal");
        vertical += Input.GetAxis("Vertical");

        // Move player on X and Z axes only (keep Y constant)
        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;
        transform.position += movement * moveSpeed * Time.deltaTime;
    }
}
