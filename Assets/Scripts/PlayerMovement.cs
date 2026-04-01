using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float lockedY = 0f;
    [SerializeField] private bool useStartingY = true;
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParameter = "isWalking";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float inputDeadzone = 0.1f;

    private bool hasAnimator;
    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector3 lastMoveDirection = Vector3.forward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        hasAnimator = animator != null;
    }

    private void Start()
    {
        if (useStartingY)
            lockedY = transform.position.y;

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        Vector3 startPos = rb.position;
        startPos.y = lockedY;
        rb.position = startPos;

        lastMoveDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
    }

    private void Update()
    {
        ReadMovementInput();
        UpdateAnimation(moveInput.sqrMagnitude > 0.0001f);
    }

    private void FixedUpdate()
    {
        Vector3 targetVelocity = moveInput * moveSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        rb.angularVelocity = Vector3.zero;

        Vector3 pos = rb.position;
        pos.y = lockedY;
        rb.position = pos;

        if (moveInput.sqrMagnitude > 0.0001f)
            lastMoveDirection = moveInput;

        if (lastMoveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lastMoveDirection, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * 360f * Time.fixedDeltaTime);
            rb.MoveRotation(nextRotation);
        }
    }

    private void ReadMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        bool wPressed = Input.GetKey(KeyCode.W);
        bool sPressed = Input.GetKey(KeyCode.S);
        bool aPressed = Input.GetKey(KeyCode.A);
        bool dPressed = Input.GetKey(KeyCode.D);

        if (wPressed == sPressed)
            vertical = Mathf.Abs(vertical) > inputDeadzone ? Mathf.Sign(vertical) : 0f;
        else
            vertical = wPressed ? 1f : -1f;

        if (aPressed == dPressed)
            horizontal = Mathf.Abs(horizontal) > inputDeadzone ? Mathf.Sign(horizontal) : 0f;
        else
            horizontal = aPressed ? -1f : 1f;

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void UpdateAnimation(bool isMoving)
    {
        if (!hasAnimator)
            return;

        if (!string.IsNullOrEmpty(isMovingParameter))
            animator.SetBool(isMovingParameter, isMoving);

        if (!string.IsNullOrEmpty(speedParameter))
            animator.SetFloat(speedParameter, isMoving ? 1f : 0f);
    }
}