using UnityEngine;

// Este script maneja tanto el movimiento como las animaciones del jugador.
// Requiere que el GameObject tenga un CharacterController y un Animator.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────
    // CONFIGURACIÓN (editables desde el Inspector de Unity)
    // ─────────────────────────────────────────

    [Header("Movimiento")]
    public float walkSpeed = 3f;       // Velocidad al caminar
    public float runSpeed  = 6f;       // Velocidad al correr
    public float jumpForce = 5f;       // Fuerza del salto
    public float gravity   = -9.81f;  // Gravedad aplicada al personaje

    [Header("Run To Stop")]
    public float runToStopDuration = 0.3f; // Segundos que dura la animación de frenada

    // ─────────────────────────────────────────
    // REFERENCIAS INTERNAS (se obtienen automáticamente)
    // ─────────────────────────────────────────

    private CharacterController _cc;       // Componente que mueve al personaje
    private Animator            _anim;     // Componente que controla las animaciones
    private Transform           _camTransform; // Posición de la cámara (para moverse en su dirección)

    // ─────────────────────────────────────────
    // ESTADO INTERNO
    // ─────────────────────────────────────────

    private Vector3 _velocity;           // Velocidad acumulada (usada para la gravedad y el salto)
    private bool    _wasRunning;         // ¿Estaba corriendo el frame anterior?
    private float   _runToStopTimer;     // Contador para la animación de Run To Stop
    private bool    _isRunToStop;        // ¿Está en estado Run To Stop ahora mismo?

    // Nombres de los parámetros del Animator Controller
    // Si los renombrás en Unity, cambiá estas strings también
    private static readonly int ParamSpeed    = Animator.StringToHash("Speed");
    private static readonly int ParamGrounded = Animator.StringToHash("Grounded");
    private static readonly int ParamJump     = Animator.StringToHash("Jump");

    // ─────────────────────────────────────────
    // INICIO
    // ─────────────────────────────────────────

    private void Start()
    {
        _cc   = GetComponent<CharacterController>();
        _anim = GetComponent<Animator>();

        // Buscamos la cámara principal de la escena
        if (Camera.main != null)
            _camTransform = Camera.main.transform;
        else
            Debug.LogWarning("PlayerController: No se encontró una cámara con tag 'MainCamera'.");
    }

    // ─────────────────────────────────────────
    // ACTUALIZACIÓN (se ejecuta una vez por frame)
    // ─────────────────────────────────────────

    private void Update()
    {
        HandleMovement();
        HandleJump();
        ApplyGravity();
        HandleAnimations();
    }

    // ─────────────────────────────────────────
    // MOVIMIENTO
    // ─────────────────────────────────────────

    private void HandleMovement()
    {
        // Leemos los ejes de entrada (WASD o flechas del teclado)
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D o ←/→
        float vertical   = Input.GetAxisRaw("Vertical");   // W/S o ↑/↓

        // Mantenemos shift para correr
        bool isRunInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Calculamos la dirección de movimiento relativa a la cámara
        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;
        Vector3 moveDir  = Vector3.zero;

        if (inputDir.magnitude > 0.1f && _camTransform != null)
        {
            // Movemos en la dirección que mira la cámara (ignorando el eje Y)
            Vector3 camForward = Vector3.ProjectOnPlane(_camTransform.forward, Vector3.up).normalized;
            Vector3 camRight   = Vector3.ProjectOnPlane(_camTransform.right,   Vector3.up).normalized;
            moveDir = (camForward * vertical + camRight * horizontal).normalized;

            // Rotamos el personaje para que mire hacia donde se mueve
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        // Elegimos la velocidad según si está corriendo o caminando
        float speed = (isRunInput && inputDir.magnitude > 0.1f) ? runSpeed : walkSpeed;

        // Movemos el personaje (solo en X/Z, la Y la maneja la gravedad)
        Vector3 horizontalMove = moveDir * speed;
        _cc.Move(new Vector3(horizontalMove.x, _velocity.y, horizontalMove.z) * Time.deltaTime);

        // Guardamos si estaba corriendo para detectar la frenada
        bool isMoving  = inputDir.magnitude > 0.1f;
        bool isRunning = isMoving && isRunInput;

        // Detectar Run To Stop: venía corriendo y ahora se detuvo
        if (_wasRunning && !isRunning && _cc.isGrounded)
        {
            _isRunToStop    = true;
            _runToStopTimer = runToStopDuration;
        }

        _wasRunning = isRunning;

        // Bajamos el timer de Run To Stop
        if (_isRunToStop)
        {
            _runToStopTimer -= Time.deltaTime;
            if (_runToStopTimer <= 0f)
                _isRunToStop = false;
        }

        // Guardamos la velocidad horizontal para pasarla al Animator
        _currentSpeed   = isMoving ? speed : 0f;
        _isRunningFrame = isRunning;
        _isMovingFrame  = isMoving;
    }

    // Variables temporales compartidas entre HandleMovement y HandleAnimations
    private float _currentSpeed;
    private bool  _isRunningFrame;
    private bool  _isMovingFrame;

    // ─────────────────────────────────────────
    // SALTO
    // ─────────────────────────────────────────

    private void HandleJump()
    {
        // Solo se puede saltar si está en el suelo
        if (Input.GetButtonDown("Jump") && _cc.isGrounded)
        {
            // Calculamos la velocidad vertical necesaria para alcanzar la altura deseada
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

            // Activamos el trigger de salto en el Animator
            _anim.SetTrigger(ParamJump);
        }
    }

    // ─────────────────────────────────────────
    // GRAVEDAD
    // ─────────────────────────────────────────

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _velocity.y < 0f)
        {
            // Pequeño valor negativo para mantener el personaje pegado al suelo
            _velocity.y = -2f;
        }
        else
        {
            // Aplicamos gravedad acumulativa mientras está en el aire
            _velocity.y += gravity * Time.deltaTime;
        }
    }

    // ─────────────────────────────────────────
    // ANIMACIONES
    // ─────────────────────────────────────────

    private void HandleAnimations()
    {
        bool isGrounded = _cc.isGrounded;
        bool isFalling  = !isGrounded && _velocity.y < 0f;

        // --- Parámetro Speed ---
        // 0   = Idle
        // 0.5 = Walk
        // 1   = Run
        // 1.5 = Run To Stop (podés usar este valor para la transición en el Animator)
        float animSpeed;

        if (_isRunToStop && isGrounded)
        {
            animSpeed = 1.5f; // Run To Stop
        }
        else if (_isRunningFrame)
        {
            animSpeed = 1f;   // Run
        }
        else if (_isMovingFrame)
        {
            animSpeed = 0.5f; // Walk
        }
        else
        {
            animSpeed = 0f;   // Idle
        }

        _anim.SetFloat(ParamSpeed,    animSpeed);
        _anim.SetBool (ParamGrounded, isGrounded);

        // Nota: el Trigger "Jump" ya se setea en HandleJump() al momento del salto.
        // La animación de Falling se puede controlar en el Animator con:
        //   Grounded = false  AND  velocidad Y < 0
        // O bien podés agregar un Bool "IsFalling" si preferís manejarlo desde acá.
        // Ejemplo (descomentá si lo necesitás):
        // _anim.SetBool("IsFalling", isFalling);
    }
}