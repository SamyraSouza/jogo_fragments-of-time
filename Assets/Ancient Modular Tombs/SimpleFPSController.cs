using UnityEngine;
using UnityEngine.InputSystem; 

public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Default walking speed")]
    public float walkSpeed = 4.0f;
    [Tooltip("Running speed when Left Shift is pressed")]
    public float runSpeed = 7.5f;
    [Tooltip("Smoothness of transition between walking and running states")]
    public float accelerationSmooth = 8.0f;
    public float jumpForce = 5.0f;
    [Tooltip("Desligado: Espaço é o soco (PlayerCombat) e não pula")]
    public bool jumpOnSpace = false;

    [Header("Mouse Settings (Smooth & Calibrated)")]
    [Range(0.005f, 0.1f)]
    [Tooltip("Mouse sensitivity")]
    public float lookSpeed = 0.02f; 
    [Tooltip("Camera lag/inertia. Lower values mean smoother, more cinematic movement (Recommended: 5-15)")]
    public float cameraSmoothing = 10.0f;
    [Range(10f, 85f)]
    [Tooltip("Clamps vertical look angle to prevent camera flipping over completely")]
    public float lookXLimit = 75.0f;

    [Header("References")]
    public Light flashlight;
    public Camera playerCamera;

    // Internal variables for handling speed and rotation accumulation
    private float currentSpeed;
    private float targetRotationX = 0f;
    private float targetRotationY = 0f;

    private Quaternion bodyTargetRot = Quaternion.identity;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private Vector3 moveInput = Vector3.zero;
    [HideInInspector] public float rollTilt;
    private Vector3 pushVel;
    private float slowUntil, slowFactor = 1f;

    /// <summary>Anda mais devagar por um tempo (factor 0..1).</summary>
    public void Slow(float seconds, float factor) { slowUntil = Time.time + seconds; slowFactor = factor; }
    private float pushUntil;

    /// <summary>Empurrão horizontal por um instante (rolamento): sobrepõe a velocidade de andar.</summary>
    public void Push(Vector3 velocity, float seconds) { pushVel = velocity; pushUntil = Time.time + seconds; }

    void Start()
    {
        // Lock and hide the system mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCamera == null)
        {
            Debug.LogError("Camera not assigned! Please assign a camera in the inspector.");
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null) capsuleCollider = gameObject.AddComponent<CapsuleCollider>();

        // CRITICAL for smooth movement: enable physics interpolation!
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // sem atrito: não gruda nas paredes ao andar junto delas
        capsuleCollider.sharedMaterial = new PhysicsMaterial("PlayerSlide")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        // Initialize starting movement speed
        currentSpeed = walkSpeed;

        // Safely cache starting rotation values to prevent violent camera jumps on awake
        targetRotationY = transform.rotation.eulerAngles.y;
        bodyTargetRot = Quaternion.Euler(0, targetRotationY, 0);
        
        float startX = playerCamera.transform.localRotation.eulerAngles.x;
        if (startX > 180) startX -= 360;
        targetRotationX = startX;
    }

    /// <summary>Teletransporta o corpo (Rigidbody com interpolação ignora transform.position) e alinha o giro.</summary>
    public void TeleportTo(Vector3 pos, float yaw)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        targetRotationY = yaw;
        bodyTargetRot = Quaternion.Euler(0f, yaw, 0f);
        transform.SetPositionAndRotation(pos, bodyTargetRot);
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = bodyTargetRot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
    }

    /// <summary>Gira suavemente a visão para um ponto do mundo (blend 0..1 por chamada).</summary>
    public void LookAtPoint(Vector3 point, float blend)
    {
        if (playerCamera == null) return;
        Vector3 d = point - playerCamera.transform.position;
        float horiz = new Vector2(d.x, d.z).magnitude;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Clamp(-Mathf.Atan2(d.y, horiz) * Mathf.Rad2Deg, -lookXLimit, lookXLimit);
        targetRotationY = Mathf.LerpAngle(targetRotationY, yaw, blend);
        targetRotationX = Mathf.Lerp(targetRotationX, pitch, blend);
    }

    void Update()
    {
        GatherMovementInput();
        LookAroundSlerp();
        ToggleFlashlight();
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    void GatherMovementInput()
    {
        if (Keyboard.current == null) return;

        float moveForward = 0;
        float moveSideways = 0;

        // Collect raw keyboard inputs
        if (Keyboard.current.wKey.isPressed) moveForward += 1;
        if (Keyboard.current.sKey.isPressed) moveForward -= 1;

        if (Keyboard.current.dKey.isPressed) moveSideways += 1;
        if (Keyboard.current.aKey.isPressed) moveSideways -= 1;

        // Calculate combined raw movement direction relative to player orientation
        moveInput = (transform.right * moveSideways) + (transform.forward * moveForward);
        
        // Normalize vector to ensure diagonal movement isn't faster
        if (moveInput.magnitude > 1f)
        {
            moveInput.Normalize();
        }

        // SPEED HANDLING: Smoothly transition speed based on Left Shift state
        float targetSpeed = Keyboard.current.leftShiftKey.isPressed ? runSpeed : walkSpeed;
        if (Time.time < slowUntil) targetSpeed *= slowFactor;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelerationSmooth * Time.deltaTime);

        // Jump trigger logic
        if (jumpOnSpace && Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            // Reset vertical velocity prior to adding impulse for consistent jump heights
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void ApplyMovement()
    {
        // Anda por velocidade (a física resolve as colisões): MovePosition num Rigidbody dinâmico atravessa paredes e treme nelas.
        Vector3 targetVelocity = Time.time < pushUntil ? pushVel : moveInput * currentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, bodyTargetRot, cameraSmoothing * Time.fixedDeltaTime));
    }

    void LookAroundSlerp()
    {
        if (playerCamera == null || Mouse.current == null) return;

        // Fetch mouse delta values for the current frame
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Accumulate target rotation angles
        targetRotationX -= mouseDelta.y * lookSpeed;
        targetRotationY += mouseDelta.x * lookSpeed;

        // Hard clamp vertical vision to avoid unsteadiness or inversion loops
        targetRotationX = Mathf.Clamp(targetRotationX, -lookXLimit, lookXLimit);

        // Convert raw angles to Quaternions to safely bypass Unity's 0-360 euler limits
        Quaternion targetCamRot = Quaternion.Euler(targetRotationX, 0, rollTilt);
        Quaternion targetBodyRot = Quaternion.Euler(0, targetRotationY, 0);

        // Smoothly interpolate current object rotations toward targeted quaternions using Slerp
        playerCamera.transform.localRotation = Quaternion.Slerp(
            playerCamera.transform.localRotation, 
            targetCamRot, 
            cameraSmoothing * Time.deltaTime
        );

        // Corpo gira via Rigidbody no FixedUpdate (evita tremor com a interpolação)
        bodyTargetRot = targetBodyRot;
    }

    void ToggleFlashlight()
    {
        if (Keyboard.current == null || flashlight == null) return;

        // Handle toggle action for the flashlight via F key
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            flashlight.enabled = !flashlight.enabled;
        }
    }

    bool IsGrounded()
    {
        // Cast a short ray downward from the center of the controller to detect the floor
        float rayDistance = capsuleCollider.bounds.extents.y + 0.1f;
        return Physics.Raycast(transform.position, Vector3.down, rayDistance);
    }
}