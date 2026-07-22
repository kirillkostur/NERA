using NERA.Inventory;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 3.5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 6f;
    [SerializeField, Min(0f)] private float crouchSpeed = 1.8f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;

    [Header("Jump & Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -35f;
    [SerializeField] private float groundedGravity = -5f;
    [SerializeField, Min(0f)] private float groundedGraceTime = 0.12f;

    [Header("Crouch")]
    [SerializeField, Min(0.1f)] private float crouchHeight = 1.1f;
    [SerializeField, Min(0f)] private float crouchSmooth = 12f;

    [Header("Stamina")]
    [SerializeField, Min(0.1f)] private float maxStamina = 5f;
    [SerializeField, Min(0f)] private float staminaDrainRate = 1f;
    [SerializeField, Min(0f)] private float staminaRecoveryRate = 1.5f;
    [SerializeField, Min(0f)] private float minStaminaToSprint = 0.2f;

    private CharacterController characterController;
    private PlayerFollowCamera followCamera;
    private PlayerEquipmentController equipmentController;

    private float standingHeight;
    private float verticalVelocity;
    private float currentStamina;
    private float lastGroundedTime;
    private Vector2 movementInput;

    private bool isGrounded;
    private bool isMoving;
    private bool isSprinting;
    private bool isCrouching;
    private bool inputEnabled = true;
    private bool isDead;

    private static readonly int AnimatorMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int AnimatorCrouchMoveSpeed = Animator.StringToHash("CrouchMoveSpeed");
    private static readonly int AnimatorCrouch = Animator.StringToHash("Crouch");
    private static readonly int AnimatorGrounded = Animator.StringToHash("Grounded");
    private static readonly int AnimatorJump = Animator.StringToHash("Jump");
    private static readonly int AnimatorAim = Animator.StringToHash("Aim");
    private static readonly int AnimatorWeaponAim = Animator.StringToHash("WeaponAim");
    private static readonly int AnimatorAimX = Animator.StringToHash("AimX");
    private static readonly int AnimatorAimY = Animator.StringToHash("AimY");
    private static readonly int AnimatorSprint = Animator.StringToHash("Sprint");

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsGrounded => isGrounded;
    public bool IsMoving => isMoving;
    public bool IsSprinting => isSprinting;
    public bool IsCrouching => isCrouching;
    public bool IsDead => isDead;
    public Animator Animator => animator;
    public CharacterController CharacterController => characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        equipmentController = GetComponent<PlayerEquipmentController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        ResolveFollowCamera();
        standingHeight = characterController.height;
        currentStamina = maxStamina;
    }

    private void Update()
    {
        if (isDead)
            return;

        if (!inputEnabled)
        {
            UpdateWhileInputDisabled();
            return;
        }

        UpdateGroundedBeforeMove();
        UpdateCrouch();
        UpdateJumpAndGravity();
        Move();
        UpdateStamina();
        UpdateAnimator();
    }

    private void UpdateWhileInputDisabled()
    {
        if (characterController == null || !characterController.enabled)
            return;

        UpdateGroundedBeforeMove();

        if (!isGrounded)
            verticalVelocity += gravity * Time.deltaTime;

        CollisionFlags collisionFlags = characterController.Move(
            Vector3.up * verticalVelocity * Time.deltaTime);

        if ((collisionFlags & CollisionFlags.Below) != 0)
        {
            isGrounded = true;
            verticalVelocity = groundedGravity;
        }

        isMoving = false;
        isSprinting = false;
        isCrouching = false;
        ApplyIdleAnimatorParameters();
    }

    private void UpdateGroundedBeforeMove()
    {
        isGrounded = characterController.isGrounded;

        if (!isGrounded)
            return;

        lastGroundedTime = Time.time;

        if (verticalVelocity < 0f)
            verticalVelocity = groundedGravity;
    }

    private void UpdateCrouch()
    {
        isCrouching = Input.GetKey(KeyCode.LeftControl);
        float targetHeight = isCrouching ? crouchHeight : standingHeight;

        characterController.height = Mathf.Lerp(
            characterController.height,
            targetHeight,
            crouchSmooth * Time.deltaTime);

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
            lastGroundedTime = float.NegativeInfinity;

            if (animator != null)
                animator.SetTrigger(AnimatorJump);
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void Move()
    {
        movementInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        if (movementInput.sqrMagnitude > 1f)
            movementInput.Normalize();

        Vector3 input = new Vector3(
            movementInput.x,
            0f,
            movementInput.y);

        isMoving = input.sqrMagnitude > 0.01f;
        isSprinting = CanSprint();
        bool isAiming = IsAiming();

        Vector3 moveDirection = Vector3.zero;

        if (isMoving)
        {
            moveDirection = GetCameraRelativeDirection(input);
            if (!isAiming)
                RotateToDirection(moveDirection);
        }

        if (isAiming)
            RotateToCameraForward();

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
            currentStamina = Mathf.Max(
                0f,
                currentStamina - staminaDrainRate * Time.deltaTime);
            return;
        }

        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + staminaRecoveryRate * Time.deltaTime);
    }

    private float GetCurrentSpeed()
    {
        if (isCrouching)
            return crouchSpeed;

        return isSprinting ? sprintSpeed : walkSpeed;
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
            rotationSpeed * Time.deltaTime);
    }

    private void RotateToCameraForward()
    {
        Vector3 direction = GetCameraForward();
        if (direction.sqrMagnitude < 0.01f)
            return;

        RotateToDirection(direction);
    }

    private bool IsAiming()
    {
        if (isCrouching)
            return false;

        if (followCamera == null)
            ResolveFollowCamera();

        return followCamera != null && followCamera.IsAiming;
    }

    private void ResolveFollowCamera()
    {
        if (cameraTransform != null)
            followCamera = cameraTransform.GetComponent<PlayerFollowCamera>();

        if (followCamera == null)
            followCamera = FindFirstObjectByType<PlayerFollowCamera>();
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        bool isAiming = IsAiming();
        bool isWeaponAiming = isAiming && HasEquippedWeapon();
        float aimLocomotionScale = isSprinting ? 1f : 0.5f;

        animator.SetBool(AnimatorCrouch, isCrouching);
        animator.SetBool(AnimatorGrounded, isGrounded);
        animator.SetBool(AnimatorAim, isAiming);
        animator.SetBool(AnimatorWeaponAim, isWeaponAiming);
        animator.SetBool(AnimatorSprint, isSprinting);
        animator.SetFloat(AnimatorMoveSpeed, GetNormalMoveSpeed(), 0.1f, Time.deltaTime);
        animator.SetFloat(AnimatorCrouchMoveSpeed, GetCrouchMoveSpeed(), 0.1f, Time.deltaTime);
        animator.SetFloat(
            AnimatorAimX,
            isAiming ? movementInput.x * aimLocomotionScale : 0f,
            0.1f,
            Time.deltaTime);
        animator.SetFloat(
            AnimatorAimY,
            isAiming ? movementInput.y * aimLocomotionScale : 0f,
            0.1f,
            Time.deltaTime);
    }

    private float GetNormalMoveSpeed()
    {
        if (isCrouching || !isMoving)
            return 0f;

        return isSprinting ? 1f : 0.6f;
    }

    private float GetCrouchMoveSpeed()
    {
        return isCrouching && isMoving ? 1f : 0f;
    }

    private bool HasEquippedWeapon()
    {
        if (equipmentController == null)
            equipmentController = GetComponent<PlayerEquipmentController>();

        return equipmentController != null
            && equipmentController.HasEquippedWeapon;
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
        ResolveFollowCamera();
    }

    public void SetInputEnabled(bool enabled)
    {
        if (isDead)
            return;

        inputEnabled = enabled;

        if (inputEnabled)
            return;

        ResetMovementState();
        ApplyIdleAnimatorParameters();
    }

    public void HandleDeath()
    {
        if (isDead)
            return;

        isDead = true;
        inputEnabled = false;
        verticalVelocity = 0f;
        ResetMovementState();

        if (characterController != null)
            characterController.enabled = false;
    }

    private void ResetMovementState()
    {
        movementInput = Vector2.zero;
        isMoving = false;
        isSprinting = false;
        isCrouching = false;
    }

    private void ApplyIdleAnimatorParameters()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(AnimatorJump);
        animator.SetBool(AnimatorCrouch, false);
        animator.SetBool(AnimatorGrounded, true);
        animator.SetBool(AnimatorAim, false);
        animator.SetBool(AnimatorWeaponAim, false);
        animator.SetBool(AnimatorSprint, false);
        animator.SetFloat(AnimatorMoveSpeed, 0f);
        animator.SetFloat(AnimatorCrouchMoveSpeed, 0f);
        animator.SetFloat(AnimatorAimX, 0f);
        animator.SetFloat(AnimatorAimY, 0f);
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(0.1f, maxStamina);
        minStaminaToSprint = Mathf.Clamp(minStaminaToSprint, 0f, maxStamina);
        crouchHeight = Mathf.Max(0.1f, crouchHeight);
    }
}
