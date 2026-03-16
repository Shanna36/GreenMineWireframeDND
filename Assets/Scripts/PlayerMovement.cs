using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("How fast the character turns to face movement direction.")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Physics")]
    [Tooltip("Small downward force to keep the controller grounded. Keep this low for a flat factory floor.")]
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Animation")]
    [Tooltip("Animator on the character root. If left empty, will try GetComponentInChildren<Animator>().")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator parameter used for movement blend (float). Defaults to 'Speed'.")]
    [SerializeField] private string speedParam = "Speed";

    private CharacterController controller;
    private int speedParamHash;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        speedParamHash = Animator.StringToHash(speedParam);
    }

    private void Update()
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

        if (Input.GetKey(KeyCode.W)) vertical += 1f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;

        horizontal += Input.GetAxis("Horizontal");
        vertical += Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontal, 0f, vertical);
        if (movement.sqrMagnitude > 1f)
            movement.Normalize();

        return movement;
    }

    private void ApplyMovement(Vector3 movement)
    {
        if (controller == null)
            return;

        if (controller.isGrounded)
        {
            verticalVelocity = groundedStickForce;
        }

        Vector3 finalMovement = movement * moveSpeed;
        finalMovement.y = verticalVelocity;

        controller.Move(finalMovement * Time.deltaTime);
    }

    private void ApplyRotation(Vector3 movement)
    {
        if (movement.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimation(Vector3 movement)
    {
        if (animator == null)
            return;

        float speed01 = Mathf.Clamp01(movement.magnitude);
        animator.SetFloat(speedParamHash, speed01, 0.1f, Time.deltaTime);
    }
}
