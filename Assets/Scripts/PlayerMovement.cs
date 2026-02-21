using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("How fast the character turns to face movement direction.")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Animation")]
    [Tooltip("Animator on the character root. If left empty, will try GetComponentInChildren<Animator>().")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator parameter used for movement blend (float). Defaults to 'Speed'.")]
    [SerializeField] private string speedParam = "Speed";

    private int speedParamHash;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        speedParamHash = Animator.StringToHash(speedParam);
    }

    void Update()
    {
        Vector3 movement = GetMovementInput();
        ApplyMovement(movement);
        ApplyRotation(movement);
        UpdateAnimation(movement);
    }

    private Vector3 GetMovementInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        // WASD input
        if (Input.GetKey(KeyCode.W)) vertical += 1f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;

        // Arrow key / Input Manager axes (adds support for gamepad too)
        horizontal += Input.GetAxis("Horizontal");
        vertical += Input.GetAxis("Vertical");

        // Move on X and Z axes only (keep Y constant)
        Vector3 movement = new Vector3(horizontal, 0f, vertical);
        if (movement.sqrMagnitude > 1f)
            movement.Normalize();

        return movement;
    }

    private void ApplyMovement(Vector3 movement)
    {
        transform.position += movement * moveSpeed * Time.deltaTime;
    }

    private void ApplyRotation(Vector3 movement)
    {
        // Only rotate when we actually have input
        if (movement.sqrMagnitude < 0.0001f)
            return;

        // Face the direction we're moving
        Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimation(Vector3 movement)
    {
        if (animator == null)
            return;

        // 0 when idle, 1 when moving (you can scale this later if you add sprinting)
        float speed01 = Mathf.Clamp01(movement.magnitude);
        animator.SetFloat(speedParamHash, speed01, 0.1f, Time.deltaTime);
    }
}
