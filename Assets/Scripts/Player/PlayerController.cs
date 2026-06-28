using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.8f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -35f;
    [SerializeField] private float groundedGravity = -5f;
    [SerializeField] private float groundedGraceTime = 0.12f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float crouchSmooth = 12f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrainRate = 1f;
    [SerializeField] private float staminaRecoveryRate = 1.5f;
    [SerializeField] private float minStaminaToSprint = 0.2f;

    private CharacterController characterController;

    private float standingHeight;
    private float verticalVelocity;
    private float currentStamina;
    private float lastGroundedTime;

    private bool isGrounded;
    private bool isMoving;
    private bool isSprinting;
    private bool isCrouching;

    private static readonly int AnimatorMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int AnimatorCrouchMoveSpeed = Animator.StringToHash("CrouchMoveSpeed");
    private static readonly int AnimatorCrouch = Animator.StringToHash("Crouch");
    private static readonly int AnimatorGrounded = Animator.StringToHash("Grounded");
    private static readonly int AnimatorJump = Animator.StringToHash("Jump");

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    public bool IsGrounded => isGrounded;
    public bool IsMoving => isMoving;
    public bool IsSprinting => isSprinting;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        standingHeight = characterController.height;
        currentStamina = maxStamina;
    }

    private void Update()
    {
        UpdateGroundedBeforeMove();
        UpdateCrouch();
        UpdateJumpAndGravity();
        Move();
        UpdateStamina();
        UpdateAnimator();
    }

    private void UpdateGroundedBeforeMove()
    {
        isGrounded = characterController.isGrounded;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;

            if (verticalVelocity < 0f)
                verticalVelocity = groundedGravity;
        }
    }

    private void UpdateCrouch()
    {
        isCrouching = Input.GetKey(KeyCode.LeftControl);

        float targetHeight = isCrouching ? crouchHeight : standingHeight;

        characterController.height = Mathf.Lerp(
            characterController.height,
            targetHeight,
            crouchSmooth * Time.deltaTime
        );

        Vector3 center = characterController.center;
        center.y = characterController.height * 0.5f;
        characterController.center = center;
    }

    private void UpdateJumpAndGravity()
    {
        bool canJump = isGrounded || Time.time - lastGroundedTime <= groundedGraceTime;

        if (Input.GetKeyDown(KeyCode.Space) && canJump && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false;
            lastGroundedTime = -999f;

            if (animator != null)
                animator.SetTrigger(AnimatorJump);
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void Move()
    {
        Vector3 input = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        isMoving = input.sqrMagnitude > 0.01f;
        isSprinting = CanSprint();

        Vector3 moveDirection = Vector3.zero;

        if (isMoving)
        {
            moveDirection = GetCameraRelativeDirection(input);
            RotateToDirection(moveDirection);
        }

        Vector3 velocity = moveDirection * GetCurrentSpeed();
        velocity.y = verticalVelocity;

        CollisionFlags collisionFlags = characterController.Move(velocity * Time.deltaTime);

        bool hitGround = (collisionFlags & CollisionFlags.Below) != 0;

        if (hitGround)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;

            if (verticalVelocity < 0f)
                verticalVelocity = groundedGravity;
        }
        else
        {
            isGrounded = false;
        }
    }

    private bool CanSprint()
    {
        return Input.GetKey(KeyCode.LeftShift)
            && isMoving
            && !isCrouching
            && currentStamina > minStaminaToSprint;
    }

    private void UpdateStamina()
    {
        if (isSprinting)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(currentStamina, 0f);
            return;
        }

        currentStamina += staminaRecoveryRate * Time.deltaTime;
        currentStamina = Mathf.Min(currentStamina, maxStamina);
    }

    private float GetCurrentSpeed()
    {
        if (isCrouching)
            return crouchSpeed;

        if (isSprinting)
            return sprintSpeed;

        return walkSpeed;
    }

    private Vector3 GetCameraRelativeDirection(Vector3 input)
    {
        Vector3 direction = GetCameraForward() * input.z + GetCameraRight() * input.x;
        direction.y = 0f;

        return direction.normalized;
    }

    private Vector3 GetCameraForward()
    {
        if (cameraTransform == null)
            return transform.forward;

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;

        return forward.sqrMagnitude > 0.01f
            ? forward.normalized
            : transform.forward;
    }

    private Vector3 GetCameraRight()
    {
        if (cameraTransform == null)
            return transform.right;

        Vector3 right = cameraTransform.right;
        right.y = 0f;

        return right.sqrMagnitude > 0.01f
            ? right.normalized
            : transform.right;
    }

    private void RotateToDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetBool(AnimatorCrouch, isCrouching);
        animator.SetBool(AnimatorGrounded, isGrounded);

        animator.SetFloat(AnimatorMoveSpeed, GetNormalMoveSpeed(), 0.1f, Time.deltaTime);
        animator.SetFloat(AnimatorCrouchMoveSpeed, GetCrouchMoveSpeed(), 0.1f, Time.deltaTime);
    }

    private float GetNormalMoveSpeed()
    {
        if (isCrouching || !isMoving)
            return 0f;

        return isSprinting ? 1f : 0.6f;
    }

    private float GetCrouchMoveSpeed()
    {
        if (!isCrouching || !isMoving)
            return 0f;

        return 1f;
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }
}
