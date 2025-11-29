using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class MovJugador1 : MonoBehaviour
{
    [Header("Velocidades")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float crouchSpeed = 6.4f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Altura del Collider")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private Vector3 standCenter = new Vector3(0, 1f, 0);
    [SerializeField] private Vector3 crouchCenter = new Vector3(0, 0.5f, 0);

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float runCooldown = 4f;
    [SerializeField] private float staminaDepletionRate = 12f;
    [SerializeField] private float staminaRechargeRate = 12f;

    [Header("Aceleracion")]
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 16f;
    private float currentSpeedScalar;

    [Header("Collection Settings")]
    [SerializeField] private float collectionRange = 2f;
    [SerializeField] private LayerMask collectableLayer;
    [SerializeField] private InputActionReference collectAction;

    [Header("Door Lift Settings")]
    [SerializeField] private InputActionReference liftDoorAction;

    [Header("Visual Effect - Fog Sphere")]
    [Tooltip("Referencia al Visual Effect del jugador")]
    [SerializeField] private VisualEffect fogSphereVFX;

    [Tooltip("Nombre del parametro Vector3 en el VFX Graph")]
    [SerializeField] private string vfxCenterParameterName = "SpherePosition";

    [Tooltip("Offset vertical de la esfera respecto al jugador")]
    [SerializeField] private Vector3 sphereOffset = new Vector3(0, 1f, 0);

    [Header("Inventory Settings")]
    [SerializeField] private GameObject inventoryCanvas;
    [SerializeField] private InputActionReference inventoryAction;

    [Header("Audio (Animation Events)")]
    [Tooltip("Clip de sonido de paso para caminar.")]
    public AudioClip walkFootstepClip;
    [Tooltip("Clip de sonido de paso para correr.")]
    public AudioClip runFootstepClip;
    [Tooltip("Clip de sonido de paso para agacharse.")]
    public AudioClip crouchFootstepClip;

    [Header("Popup Flotante sobre cabeza")]
    [SerializeField] private PlayerPopupBillboard popupBillboard;

    private CharacterController controller;
    private Animator animator;
    private PlayerInventory playerInventory;
    private PlayerStaminaUI staminaUI;

    private Vector2 moveInput;
    private bool isRunningInput = false;
    private bool isCrouching = false;
    private bool isMoving = false;

    private float currentStamina;
    private float cooldownTimer = 0f;
    private bool canRun = true;
    private Vector3 verticalVelocity;

    private bool wasRunning = false;
    private bool staminaWasEmpty = false;

    private bool isInUI = false;
    private KeypadUIManager currentLockUI = null;
    private PlayerInput playerInput;

    private FallenDoor currentDoorToLift = null;
    private bool isHoldingDoor = false;
    private bool isAnimationInLiftState = false;
    private float liftButtonHoldTime = 0f;
    private bool liftButtonPressed = false;
    [SerializeField] private float minHoldTimeToStartLift = 0.15f;

    private PuertaDobleAccion currentDoor = null;
    private ElectricBox currentElectricBox = null;
    private PuertaDobleConLlave currentKeyDoor = null;

    private Transform cameraTransform;
    private Vector3 originalCameraPosition;
    private bool isShaking = false;
    private Gamepad gamepad;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        playerInventory = GetComponent<PlayerInventory>();
        staminaUI = GetComponent<PlayerStaminaUI>();

        currentStamina = maxStamina;

        controller.height = standHeight;
        controller.center = standCenter;

        if (staminaUI != null)
        {
            staminaUI.InitializeMaxStamina(maxStamina);
        }

        if (collectAction != null)
            collectAction.action.performed += ctx => TryInteract();

        if (liftDoorAction != null)
        {
            liftDoorAction.action.performed += ctx => OnLiftDoorPressed();
            liftDoorAction.action.canceled += ctx => OnLiftDoorReleased();
        }

        if (inventoryAction != null)
            inventoryAction.action.performed += ctx => CheckForInventory();

        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.devices.Count > 0)
            gamepad = playerInput.devices[0] as Gamepad;

        cameraTransform = Camera.main?.transform;
        if (cameraTransform != null)
            originalCameraPosition = cameraTransform.localPosition;
        currentSpeedScalar = moveSpeed;
    }

    private void OnEnable()
    {
        collectAction?.action.Enable();
        liftDoorAction?.action.Enable();
        inventoryAction?.action.Enable();

        gamepad?.SetMotorSpeeds(0f, 0f);
    }

    private void OnDisable()
    {
        collectAction?.action.Disable();
        liftDoorAction?.action.Disable();
        inventoryAction?.action.Disable();

        gamepad?.SetMotorSpeeds(0f, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        
        FallenDoor doorScript = other.GetComponent<FallenDoor>();
        if (doorScript != null)
            currentDoorToLift = doorScript;

        ElectricBox box = other.GetComponentInParent<ElectricBox>() ?? other.GetComponent<ElectricBox>();
        if (box != null) currentElectricBox = box;

        PuertaDobleConLlave keyDoor = other.GetComponentInParent<PuertaDobleConLlave>() ?? other.GetComponent<PuertaDobleConLlave>();
        if (keyDoor != null) currentKeyDoor = keyDoor;

        PuertaDobleAccion door = other.GetComponentInParent<PuertaDobleAccion>() ?? other.GetComponent<PuertaDobleAccion>();
        if (door != null)
        {
            currentDoor = door;
            door.AddPlayer(this.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        FallenDoor doorScript = other.GetComponent<FallenDoor>();
        if (doorScript != null && doorScript == currentDoorToLift)
        {
            currentDoorToLift = null;
            if (isHoldingDoor || isAnimationInLiftState)
            {
                isHoldingDoor = false;
                animator.SetBool("ShouldCancelLift", true);
                StopDoorLiftEvent();
                StartCoroutine(ResetCancelLiftFlag());
            }
        }

        ElectricBox box = other.GetComponentInParent<ElectricBox>() ?? other.GetComponent<ElectricBox>();
        if (box != null && box == currentElectricBox)
            currentElectricBox = null;

        PuertaDobleConLlave keyDoor = other.GetComponentInParent<PuertaDobleConLlave>() ?? other.GetComponent<PuertaDobleConLlave>();
        if (keyDoor != null && keyDoor == currentKeyDoor)
            currentKeyDoor = null;

        PuertaDobleAccion door = other.GetComponentInParent<PuertaDobleAccion>() ?? other.GetComponent<PuertaDobleAccion>();
        if (door != null && door == currentDoor)
        {
            door.RemovePlayer(this.gameObject);
            currentDoor = null;
        }
    }

    public void StartCooperativeEffects(float shakeDuration, float shakeMagnitude, float lowFrequency, float highFrequency, float rumbleDuration)
    {
        if (cameraTransform != null)
        {
            StopCoroutine("ShakeCoroutine");
            StartCoroutine(ShakeCoroutine(shakeDuration, shakeMagnitude));
        }

        if (gamepad != null)
        {
            StopCoroutine("RumbleCoroutine");
            StartCoroutine(RumbleCoroutine(lowFrequency, highFrequency, rumbleDuration));
        }
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        if (isShaking) yield break;
        isShaking = true;
        float elapsed = 0f;

        if (originalCameraPosition == Vector3.zero && cameraTransform.parent == null)
            originalCameraPosition = cameraTransform.localPosition;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            cameraTransform.localPosition = originalCameraPosition + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraTransform.localPosition = originalCameraPosition;
        isShaking = false;
    }

    private IEnumerator RumbleCoroutine(float low, float high, float duration)
    {
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(low, high);
            yield return new WaitForSeconds(duration);
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    public void ClearCurrentDoor(PuertaDobleAccion door)
    {
        if (currentDoor == door)
            currentDoor = null;
    }

    private void OnLiftDoorPressed()
    {
        if (currentDoorToLift == null || isInUI) return;

        liftButtonPressed = true;
        liftButtonHoldTime = 0f;

        StartCoroutine(CheckLiftHold());
    }

    private void OnLiftDoorReleased()
    {
        liftButtonPressed = false;
        liftButtonHoldTime = 0f;

        if (isAnimationInLiftState || animator.GetBool("IsStartingLift"))
        {
            animator.SetBool("ShouldCancelLift", true);
            StopDoorLiftEvent();
            StartCoroutine(ResetCancelLiftFlag());
        }
    }

    private IEnumerator CheckLiftHold()
    {
        while (liftButtonPressed)
        {
            liftButtonHoldTime += Time.deltaTime;

            if (liftButtonHoldTime >= minHoldTimeToStartLift && !animator.GetBool("IsStartingLift"))
            {
                isHoldingDoor = true;
                animator.SetBool("IsStartingLift", true);
                yield break;
            }

            yield return null;
        }
    }

    public void OnDoorLiftAnimationStart()
    {
        if (currentDoorToLift == null) return;

        if (!isHoldingDoor)
        {
            animator.SetBool("ShouldCancelLift", true);
            animator.SetBool("IsStartingLift", false);
            StartCoroutine(ResetCancelLiftFlag());
            StopDoorLiftEvent();
            return;
        }

        isAnimationInLiftState = true;
    }

    public void StartDoorLiftEvent()
    {
        if (currentDoorToLift == null) return;
        isAnimationInLiftState = true;

        if (isHoldingDoor)
        {
            animator.SetBool("IsLifting", true);
            animator.SetBool("IsStartingLift", false);
            currentDoorToLift.StartLifting(this);
        }
        else
            StopDoorLiftEvent();
    }

    public void OnDoorLiftAnimationComplete()
    {
        if (currentDoorToLift == null) return;

        if (isHoldingDoor)
        {
            animator.SetBool("IsLifting", true);
            animator.SetBool("IsStartingLift", false);
            isAnimationInLiftState = true;
        }
        else
        {
            StopDoorLiftEvent();
            animator.SetBool("ShouldCancelLift", true);
            StartCoroutine(ResetCancelLiftFlag());
        }
    }

    public void StopDoorLiftEvent()
    {
        animator.SetBool("IsLifting", false);
        animator.SetBool("IsStartingLift", false);
        isAnimationInLiftState = false;
        isHoldingDoor = false;

        if (currentDoorToLift != null)
            currentDoorToLift.StopLifting();
    }

    private IEnumerator ResetCancelLiftFlag()
    {
        float waitTime = 0.15f;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("StartLift"))
            waitTime = 0.25f;

        yield return new WaitForSeconds(waitTime);
        animator.SetBool("ShouldCancelLift", false);
    }

    public void OnMove(InputValue value)
    {
        if (isInUI)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            return;
        }
        moveInput = value.Get<Vector2>();
        isMoving = moveInput.magnitude > 0.1f;
    }

    public void OnRun(InputValue value)
    {
        if (isInUI) return;
        isRunningInput = value.isPressed;
    }

    public void OnCrouch(InputValue value)
    {
        if (isInUI) return;
        if (value.isPressed)
            isCrouching = !isCrouching;
    }

    private void TryInteract()
    {
        if (isInUI) return;

        if (currentDoor != null)
        {
            if (!currentDoor.enabled || currentDoor == null)
                currentDoor = null;
            else
            {
                currentDoor.IntentoDeAccion(this.gameObject);
                return;
            }
        }

        if (currentKeyDoor != null)
        {
            currentKeyDoor.IntentoAbrirPuerta(this);
            return;
        }

        if (currentElectricBox != null)
        {
            currentElectricBox.TryDeactivatePower(this);
            return;
        }

        TryCollectItems();
    }

    private void TryCollectItems()
    {
        if (isInUI) return;

        Collider[] items = Physics.OverlapSphere(transform.position, collectionRange, collectableLayer);
        foreach (Collider itemCollider in items)
        {
            GameObject item = itemCollider.gameObject;
            if (item.GetComponent<PuertaDobleAccion>() || item.GetComponent<ElectricBox>() || item.GetComponent<PuertaDobleConLlave>())
                continue;

            PickableItem pickable = item.GetComponent<PickableItem>();
            if (pickable != null)
            {
                pickable.Collect(gameObject);
                popupBillboard?.ShowMessage($"I found the {pickable.DisplayName}!", 2f);
                return;
            }

            KeyCard keyCard = item.GetComponent<KeyCard>();
            if (keyCard != null)
            {
                keyCard.Collect(gameObject);
                popupBillboard?.ShowMessage($"I found the {keyCard.name}!", 2f);
                return;
            }

            CollectableItem collectable = item.GetComponent<CollectableItem>();
            if (collectable != null)
            {
                collectable.Collect(gameObject);
                popupBillboard?.ShowMessage($"I found the {collectable.ItemID}!", 2f);
                return;
            }
        }
    }

    public void EnterLockMode(KeypadUIManager uiManager)
    {
        if (isInUI) return;
        currentLockUI = uiManager;
        isInUI = true;
        playerInput ??= GetComponent<PlayerInput>();
        playerInput?.SwitchCurrentActionMap("UI");
    }

    public void ExitLockMode()
    {
        if (!isInUI) return;
        isInUI = false;
        currentLockUI = null;
        moveInput = Vector2.zero;
        isMoving = false;
        playerInput ??= GetComponent<PlayerInput>();
        playerInput?.SwitchCurrentActionMap("Player");
    }

    private void CheckForInventory()
    {
        if (playerInventory != null && playerInventory.HasKeyCard("Tarjeta") && !isInUI)
        {
            bool currentState = inventoryCanvas.activeSelf;
            inventoryCanvas.SetActive(!currentState);
        }
    }

    void Update()
    {
        if (fogSphereVFX != null)
        {
            Vector3 spherePosition = transform.position + sphereOffset;
            fogSphereVFX.SetVector3(vfxCenterParameterName, spherePosition);
        }

        if (isInUI || !enabled || controller == null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
            return;
        }

        float desiredSpeed;
        bool moving = moveInput.magnitude > 0.1f;

        if (currentStamina < maxStamina && !isRunningInput)
        {
            float previousStamina = currentStamina;
            currentStamina = Mathf.Clamp(currentStamina + staminaRechargeRate * Time.deltaTime, 0, maxStamina);

            if (staminaUI != null)
            {
                staminaUI.UpdateStaminaValue(currentStamina, maxStamina);
            }

            if (previousStamina < maxStamina && currentStamina >= maxStamina && staminaWasEmpty)
            {
                if (staminaUI != null)
                {
                    staminaUI.OnStaminaFullyRecharged();
                }
                staminaWasEmpty = false;
            }
        }

        if (currentStamina <= 0 && canRun)
        {
            canRun = false;
            cooldownTimer = runCooldown;
            staminaWasEmpty = true;
            isRunningInput = false;

            if (staminaUI != null)
            {
                staminaUI.HideStaminaBar();
            }
        }

        if (!moving && isRunningInput)
            isRunningInput = false;

        if (!canRun)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                canRun = true;
                currentStamina = maxStamina;

                if (staminaUI != null)
                {
                    staminaUI.UpdateStaminaValue(currentStamina, maxStamina);
                }

                if (staminaWasEmpty && staminaUI != null)
                {
                    staminaUI.OnStaminaFullyRecharged();
                    staminaWasEmpty = false;
                }
            }
        }

        if (isCrouching)
        {
            desiredSpeed = crouchSpeed;
        }
        else if (isRunningInput && moving && canRun)
        {
            desiredSpeed = runSpeed;
            currentStamina = Mathf.Clamp(currentStamina - staminaDepletionRate * Time.deltaTime, 0, maxStamina);

            if (staminaUI != null)
            {
                staminaUI.UpdateStaminaValue(currentStamina, maxStamina);
            }

            if (!wasRunning && staminaUI != null)
            {
                staminaUI.ShowStaminaBar();
                wasRunning = true;
            }
        }
        else
        {
            desiredSpeed = moveSpeed;

            if (wasRunning && staminaUI != null)
            {
                staminaUI.HideStaminaBar();
                wasRunning = false;
            }
        }

        float accel = currentSpeedScalar < desiredSpeed ? acceleration : deceleration;
        currentSpeedScalar = Mathf.MoveTowards(currentSpeedScalar, desiredSpeed, accel * Time.deltaTime);

        Vector3 movement;
        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = cameraTransform.right;
            camRight.y = 0;
            camRight.Normalize();

            movement = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            movement = new Vector3(moveInput.x, 0, moveInput.y);
        }

        if (controller.isGrounded)
            verticalVelocity.y = -2f;
        else
            verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 finalMovement = (movement * currentSpeedScalar) + new Vector3(0, verticalVelocity.y, 0);
        controller.Move(finalMovement * Time.deltaTime);

        if (movement != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        animator.SetBool("IsCrouching", isCrouching);
        animator.SetBool("IsRunning", isRunningInput && moving && canRun && !isCrouching);

        controller.height = isCrouching ? crouchHeight : standHeight;
        controller.center = isCrouching ? crouchCenter : standCenter;

        animator.SetFloat("Speed", moving ? (isRunningInput && canRun ? 2f : (isCrouching ? 0.5f : 1f)) : 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectionRange);
    }

    public void StopMovement()
    {
        this.enabled = false;
        verticalVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        isMoving = false;
        isRunningInput = false;

        if (staminaUI != null)
        {
            staminaUI.HideImmediate();
        }
        wasRunning = false;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsCrouching", false);
        }
    }

    public void AllowMovement()
    {
        this.enabled = true;
    }

    public void ResetMovementState()
    {
        this.enabled = true;
        isInUI = false;
        moveInput = Vector2.zero;
        isRunningInput = false;
        isCrouching = false;
        verticalVelocity.y = -5f;
        wasRunning = false;

        if (staminaUI != null)
        {
            staminaUI.HideImmediate();
        }
    }

    public void PlayFootstepSound(int playerID)
    {
        if (controller != null && controller.isGrounded)
        {
            AudioClip clipToPlay;
            if (isRunningInput && canRun && !isCrouching)
            {
                clipToPlay = runFootstepClip;
            }
            else if (isCrouching)
            {
                clipToPlay = crouchFootstepClip;
            }
            else
            {
                clipToPlay = walkFootstepClip;
            }

            if (clipToPlay != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(
                    clipToPlay,
                    transform.position,
                    0.3f,
                    (isRunningInput && canRun && !isCrouching) ? 1.6f : 1.4f,
                    Random.Range(0.95f, 1.05f)
                );
            }
        }
    }
}