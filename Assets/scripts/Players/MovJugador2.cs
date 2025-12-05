using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class MovJugador2 : MonoBehaviour
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

    [Header("Procedural Animation (Fatiga)")]
    [Tooltip("Referencia al script que controla el Rigging de cansancio.")]
    [SerializeField] private StaminaFatigueFeedback fatigueFeedback;

    [Header("Aceleracion")]
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 16f;
    private float currentSpeedScalar;

    [Header("Collection Settings")]
    [SerializeField] private float collectionRange = 2f;
    [SerializeField] private LayerMask collectableLayer;
    [SerializeField] private InputActionReference collectAction;

    [Header("Door Lift Settings")]
    [Tooltip("Input Action para interactuar (ej. Boton X o Cuadrado)")]
    [SerializeField] private InputActionReference liftDoorAction;
    [Tooltip("Tiempo minimo pulsando para iniciar la animacion")]
    [SerializeField] private float minHoldTimeToStartLift = 0.15f;

    [Header("Visual Effect - Fog Sphere")]
    [Tooltip("Referencia al Visual Effect del jugador")]
    [SerializeField] private VisualEffect fogSphereVFX;
    [Tooltip("Nombre del parametro Vector3 en el VFX Graph")]
    [SerializeField] private string vfxCenterParameterName = "SpherePosition";
    [Tooltip("Offset vertical de la esfera respecto al jugador")]
    [SerializeField] private Vector3 sphereOffset = new Vector3(0, 1f, 0);

    [Header("Flashlight Animation")]
    private FlashlightController flashlightController;

    [Header("Inventory Settings")]
    [SerializeField] private GameObject inventoryCanvas;
    [SerializeField] private InputActionReference inventoryAction;

    [Header("Audio (Animation Events)")]
    [Tooltip("Clip de sonido de paso para caminar.")]
    [SerializeField] private AudioClip walkFootstepClip;
    [Tooltip("Clip de sonido de paso para correr.")]
    [SerializeField] private AudioClip runFootstepClip;
    [Tooltip("Clip de sonido de paso para agacharse.")]
    [SerializeField] private AudioClip crouchFootstepClip;

    [Header("Tired State")]
    [Tooltip("Nombre del bool en el Animator para la animacion de cansado")]
    [SerializeField] private string tiredAnimationBool = "IsTired";
    [SerializeField] private AudioClip pantingSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Popup Flotante sobre cabeza")]
    [Tooltip("Arrastra aqui el objeto que tiene el script PlayerPopupBillboard")]
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

    private ElectricBox currentElectricBox = null;
    private PuertaDobleAccion currentDoor = null;
    private PuertaDobleConLlave currentKeyDoor = null;

    private FallenDoor currentDoorToLift = null;
    private bool isHoldingDoor = false;
    private bool isAnimationInLiftState = false;
    private float liftButtonHoldTime = 0f;
    private bool liftButtonPressed = false;

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

        
        if (fatigueFeedback == null) fatigueFeedback = GetComponent<StaminaFatigueFeedback>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        currentStamina = maxStamina;

        controller.height = standHeight;
        controller.center = standCenter;

        if (staminaUI != null)
        {
            staminaUI.InitializeMaxStamina(maxStamina);
        }

        if (collectAction != null)
            collectAction.action.performed += ctx => TryCollect();

        if (inventoryAction != null)
            inventoryAction.action.performed += ctx => CheckForInventory();

        if (liftDoorAction != null)
        {
            liftDoorAction.action.performed += ctx => OnLiftDoorPressed();
            liftDoorAction.action.canceled += ctx => OnLiftDoorReleased();
        }

        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.devices.Count > 0)
            gamepad = playerInput.devices[0] as Gamepad;

        cameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform != null)
            originalCameraPosition = cameraTransform.localPosition;
        currentSpeedScalar = moveSpeed;

        flashlightController = GetComponentInChildren<FlashlightController>();
    }

    private void OnEnable()
    {
        if (collectAction != null) collectAction.action.Enable();
        if (inventoryAction != null) inventoryAction.action.Enable();
        if (liftDoorAction != null) liftDoorAction.action.Enable();

        if (gamepad != null)
            gamepad.SetMotorSpeeds(0f, 0f);
    }

    private void OnDisable()
    {
        if (collectAction != null) collectAction.action.Disable();
        if (inventoryAction != null) inventoryAction.action.Disable();
        if (liftDoorAction != null) liftDoorAction.action.Disable();

        if (gamepad != null)
            gamepad.SetMotorSpeeds(0f, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        FallenDoor doorScript = other.GetComponent<FallenDoor>();
        if (doorScript != null)
        {
            currentDoorToLift = doorScript;
        }

        ElectricBox box = other.GetComponentInParent<ElectricBox>();
        if (box == null) box = other.GetComponent<ElectricBox>();
        if (box != null)
        {
            currentElectricBox = box;
        }

        PuertaDobleAccion door = other.GetComponentInParent<PuertaDobleAccion>();
        if (door == null) door = other.GetComponent<PuertaDobleAccion>();
        if (door != null)
        {
            currentDoor = door;
            door.AddPlayer(this.gameObject);
        }

        PuertaDobleConLlave keyDoor = other.GetComponentInParent<PuertaDobleConLlave>();
        if (keyDoor == null) keyDoor = other.GetComponent<PuertaDobleConLlave>();
        if (keyDoor != null)
        {
            currentKeyDoor = keyDoor;
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
                OnLiftDoorReleased();
            }
        }

        ElectricBox box = other.GetComponentInParent<ElectricBox>();
        if (box == null) box = other.GetComponent<ElectricBox>();
        if (box != null && box == currentElectricBox)
        {
            currentElectricBox = null;
        }

        PuertaDobleAccion door = other.GetComponentInParent<PuertaDobleAccion>();
        if (door == null) door = other.GetComponent<PuertaDobleAccion>();
        if (door != null && door == currentDoor)
        {
            door.RemovePlayer(this.gameObject);
            currentDoor = null;
        }

        PuertaDobleConLlave keyDoor = other.GetComponentInParent<PuertaDobleConLlave>();
        if (keyDoor == null) keyDoor = other.GetComponent<PuertaDobleConLlave>();
        if (keyDoor != null && keyDoor == currentKeyDoor)
        {
            currentKeyDoor = null;
        }
    }

    private void OnLiftDoorPressed()
    {
        if (currentDoorToLift == null || isInUI || isAnimationInLiftState) return;

        liftButtonPressed = true;
        liftButtonHoldTime = 0f;
        StartCoroutine(CheckLiftHold());
    }

    private void OnLiftDoorReleased()
    {
        liftButtonPressed = false;
        liftButtonHoldTime = 0f;

        if (isAnimationInLiftState)
        {
            if (currentDoorToLift != null)
            {
                currentDoorToLift.StopLifting();
            }

            animator.SetBool("ShouldCancelLift", true);
            animator.SetBool("IsStartingLift", false);
            animator.SetBool("IsLifting", false);

            StopCoroutine("RecoverFromCancel");
            StartCoroutine("RecoverFromCancel");
        }
    }

    private IEnumerator CheckLiftHold()
    {
        while (liftButtonPressed)
        {
            liftButtonHoldTime += Time.deltaTime;

            if (liftButtonHoldTime >= minHoldTimeToStartLift && !isAnimationInLiftState)
            {
                isHoldingDoor = true;
                isAnimationInLiftState = true;
                animator.SetBool("IsStartingLift", true);
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator RecoverFromCancel()
    {
        yield return new WaitForSeconds(0.25f);

        isAnimationInLiftState = false;
        isHoldingDoor = false;
        animator.SetBool("ShouldCancelLift", false);
    }

    public void StartDoorLiftEvent()
    {
        if (currentDoorToLift != null && isHoldingDoor)
        {
            animator.SetBool("IsStartingLift", false);
            animator.SetBool("IsLifting", true);
            currentDoorToLift.StartFailLifting(this);
        }
    }

    public void OnDoorDropFrame()
    {
        if (currentDoorToLift != null)
        {
            currentDoorToLift.StopLifting();
        }
    }

    public void OnDoorLiftAnimationComplete()
    {
        StopDoorLiftEvent();

        isAnimationInLiftState = false;
        isHoldingDoor = false;

        animator.SetBool("IsStartingLift", false);
        animator.SetBool("IsLifting", false);
        animator.SetBool("ShouldCancelLift", false);

        if (popupBillboard != null)
        {
            popupBillboard.ShowMessage("I can't lift this...", 2f);
        }
    }

    public void StopDoorLiftEvent()
    {
        if (currentDoorToLift != null)
        {
            currentDoorToLift.StopLifting();
        }
        isHoldingDoor = false;
    }

    public void ClearCurrentDoor(PuertaDobleAccion door)
    {
        if (currentDoor == door)
            currentDoor = null;
    }

    public void StartCooperativeEffects(float shakeDuration, float shakeMagnitude, float lowFrequency, float highFrequency, float rumbleDuration)
    {
        if (cameraTransform != null)
        {
            StopCoroutine("ShakeCoroutine");
            StartCoroutine(ShakeCoroutine(shakeDuration, magnitude: shakeMagnitude));
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

    private void CheckForInventory()
    {
        if (playerInventory != null && playerInventory.HasKeyCard("Card") && !isInUI)
        {
            bool currentState = inventoryCanvas.activeSelf;
            inventoryCanvas.SetActive(!currentState);
        }
    }

    public void OnMove(InputValue value)
    {
        if (isInUI)
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = value.Get<Vector2>();
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

    private void TryCollect()
    {
        if (isInUI) return;

        if (currentDoor != null)
        {
            if (!currentDoor.enabled || currentDoor == null)
            {
                currentDoor = null;
            }
            else
            {
                currentDoor.IntentoDeAccion(this.gameObject);
                return;
            }
        }

        if (currentElectricBox != null)
        {
            currentElectricBox.TryDeactivatePower(this);
            return;
        }

        if (currentKeyDoor != null)
        {
            currentKeyDoor.IntentoAbrirPuerta(this);
            return;
        }

        Collider[] items = Physics.OverlapSphere(transform.position, collectionRange, collectableLayer);
        if (items.Length == 0)
        {
            items = Physics.OverlapSphere(transform.position, collectionRange);
        }
        foreach (Collider itemCollider in items)
        {
            GameObject item = itemCollider.gameObject;

            if (item.GetComponent<PuertaDobleAccion>() != null || item.GetComponent<ElectricBox>() != null || item.GetComponent<PuertaDobleConLlave>() != null)
                continue;

            PickableItem pickable = item.GetComponent<PickableItem>();
            if (pickable != null)
            {
                pickable.Collect(gameObject);
                if (popupBillboard != null)
                    popupBillboard.ShowMessage($"I found the {pickable.DisplayName}!", 2f);
                return;
            }

            KeyCard keyCard = item.GetComponent<KeyCard>();
            if (keyCard != null)
            {
                keyCard.Collect(gameObject);
                if (popupBillboard != null)
                    popupBillboard.ShowMessage($"I found the {keyCard.name}!", 2f);
                return;
            }

            CollectableItem collectable = item.GetComponent<CollectableItem>();
            if (collectable != null)
            {
                collectable.Collect(gameObject);
                if (popupBillboard != null)
                    popupBillboard.ShowMessage($"I found the {collectable.ItemID}!", 2f);
                return;
            }
        }
    }

    public void EnterLockMode(KeypadUIManager uiManager)
    {
        if (isInUI) return;
        currentLockUI = uiManager;
        isInUI = true;

        if (controller != null) controller.enabled = false;
        if (animator != null) animator.enabled = false;

        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
    }

    public void ExitLockMode()
    {
        if (!isInUI) return;
        isInUI = false;
        currentLockUI = null;

        if (controller != null) controller.enabled = true;
        if (animator != null) animator.enabled = true;

        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");
    }

    public void StopMovement()
    {
        this.enabled = false;
        verticalVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        isRunningInput = false;

        if (staminaUI != null)
        {
            staminaUI.HideImmediate();
        }
        wasRunning = false;

        
        if (fatigueFeedback != null) fatigueFeedback.SetExhausted(false);

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

        
        if (fatigueFeedback != null) fatigueFeedback.SetExhausted(false);

        if (staminaUI != null)
        {
            staminaUI.HideImmediate();
        }
    }

    void Update()
    {
        if (fogSphereVFX != null)
        {
            Vector3 spherePosition = transform.position + sphereOffset;
            fogSphereVFX.SetVector3(vfxCenterParameterName, spherePosition);
        }

        if (isInUI || isAnimationInLiftState)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
            return;
        }

        float desiredSpeed;
        isMoving = moveInput.magnitude > 0.1f;

        
        if (currentStamina < maxStamina && !isRunningInput)
        {
            float previousStamina = currentStamina;
            currentStamina += staminaRechargeRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

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

            if (animator != null) animator.SetBool(tiredAnimationBool, true);

            
            if (fatigueFeedback != null) fatigueFeedback.SetExhausted(true);

            if (audioSource != null && pantingSound != null)
            {
                audioSource.clip = pantingSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        if (!isMoving && isRunningInput)
        {
            isRunningInput = false;
        }

        
        if (!canRun)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                canRun = true;
                currentStamina = maxStamina;

                if (animator != null) animator.SetBool(tiredAnimationBool, false);

                
                if (fatigueFeedback != null) fatigueFeedback.SetExhausted(false);

                if (audioSource != null && audioSource.isPlaying && audioSource.clip == pantingSound)
                {
                    audioSource.Stop();
                    audioSource.loop = false;
                }

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

        
        if (!canRun)
        {
            desiredSpeed = 0f;
        }
        else if (isCrouching)
        {
            desiredSpeed = crouchSpeed;

            if (wasRunning && staminaUI != null)
            {
                staminaUI.HideStaminaBar();
                wasRunning = false;
            }
        }
        else if (isRunningInput && isMoving && canRun)
        {
            desiredSpeed = runSpeed;
            if (isMoving)
            {
                currentStamina -= staminaDepletionRate * Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

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

        Vector3 movement = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0;
            camForward.Normalize();

            camRight.y = 0;
            camRight.Normalize();

            movement = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            movement = new Vector3(moveInput.x, 0, moveInput.y);
        }

        if (controller.isGrounded)
        {
            verticalVelocity.y = -2f;
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        Vector3 finalMovement = (movement * currentSpeedScalar) + new Vector3(0, verticalVelocity.y, 0);
        controller.Move(finalMovement * Time.deltaTime);

        if (movement != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (flashlightController != null)
        {
            animator.SetBool("FlashlightOn", flashlightController.isFlashlightOn);
        }

        animator.SetBool("IsCrouching", isCrouching);
        animator.SetBool("IsRunning", isRunningInput && isMoving && canRun && !isCrouching);

        if (isCrouching)
        {
            controller.height = crouchHeight;
            controller.center = crouchCenter;
            animator.SetFloat("Speed", isMoving ? 0.5f : 0f);
        }
        else
        {
            controller.height = standHeight;
            controller.center = standCenter;
            animator.SetFloat("Speed", isMoving ? 1f : 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectionRange);
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

    public void PlayFootstepSound()
    {
        PlayFootstepSound(2);
    }
}