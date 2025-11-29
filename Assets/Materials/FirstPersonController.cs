using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System.Collections;
namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
	[RequireComponent(typeof(PlayerInput))]
	public class FirstPersonController : MonoBehaviour
	{
		#region Gravity Intensification System



[Header("Dynamic Gravity Adjustment")]
public float targetStrongGravity = -40f;       // how strong gravity becomes
public float gravityTransitionSpeed = 1.5f;    // how quickly gravity changes
public float heavyMoveSpeedMultiplier = 0.7f;  // movement slowdown under strong gravity
public float heavyJumpMultiplier = 0.6f;       // lower jump height under strong gravity
public float heavyFOVChange = -5f;             // optional: reduce FOV slightly to feel “denser”
[Header("Asteroid Stick")]
public StickToAsteroid stickScript;

private float _defaultGravity;
private float _defaultMoveSpeed;
private float _defaultSprintSpeed;
private float _defaultJumpHeight;
private bool _isHeavyGravity = false;
private Coroutine gravityRoutine;

// Cinemachine references
public CinemachineVirtualCamera _virtualCam;
public CinemachineBasicMultiChannelPerlin _camNoise; 
// Handles when a gravitational wave begins
private void HandleWaveStart(GravitationalWaveManager.GravitationalWavePreset preset)
{
  
	 if (preset.name == "Tidal Forces")
    {
        Debug.Log("[FPC] Tidal Forces wave ended — restoring normal gravity.");
        ApplyStrongGravity(); // optionally return to normal or heavier gravity after
    }
}

// Handles when a gravitational wave ends
private void HandleWaveEnd(GravitationalWaveManager.GravitationalWavePreset preset)
{
     if (preset.name == "Tidal Forces")
    {
        Debug.Log("[FPC] Tidal Forces wave detected — applying reduced gravity!");
        RevertGravity(); // make gravity lighter instead of stronger
    }
}

// Optional: if you want pre-warning behavior
private void HandleWaveDetected(GravitationalWaveManager.GravitationalWavePreset preset, float timeUntilImpact)
{
    if (preset.name == "Tidal Forces")
    {
        Debug.Log($"[FPC] Incoming Tidal Forces wave in {timeUntilImpact:F1}s!");
    }
}

    void OnEnable()
    {
        GravitationalWaveManager.OnWaveStart += HandleWaveStart;
        GravitationalWaveManager.OnWaveEnd += HandleWaveEnd;
        GravitationalWaveManager.OnWaveDetected += HandleWaveDetected;
    }

    void OnDisable()
    {
        GravitationalWaveManager.OnWaveStart -= HandleWaveStart;
        GravitationalWaveManager.OnWaveEnd -= HandleWaveEnd;
        GravitationalWaveManager.OnWaveDetected -= HandleWaveDetected;
    }
private IEnumerator LandingImpact()
{
    if (_virtualCam == null) yield break;

    float duration = _isHeavyGravity ? 0.25f : 0.15f;
    float amplitude = _isHeavyGravity ? 2.5f : 1.0f;   // strength of shake
    float frequency = 2f;

    if (_camNoise != null)
    {
        _camNoise.m_AmplitudeGain = amplitude;
        _camNoise.m_FrequencyGain = frequency;
    }

    yield return new WaitForSeconds(duration);

    if (_camNoise != null)
    {
        _camNoise.m_AmplitudeGain = 0f;
        _camNoise.m_FrequencyGain = 0f;
    }
}

private void InitializeGravityDefaults()
{
    _defaultGravity = Gravity;
    _defaultMoveSpeed = MoveSpeed;
    _defaultSprintSpeed = SprintSpeed;
    _defaultJumpHeight = JumpHeight;

  
}

public void ApplyStrongGravity()
{
    if (!_isHeavyGravity)
    {
        if (gravityRoutine != null) StopCoroutine(gravityRoutine);
        gravityRoutine = StartCoroutine(AdjustGravityOverTime(true));
    }
}

public void RevertGravity()
{
    if (_isHeavyGravity)
    {
        if (gravityRoutine != null) StopCoroutine(gravityRoutine);
        gravityRoutine = StartCoroutine(AdjustGravityOverTime(false));
    }
}

private IEnumerator AdjustGravityOverTime(bool toHeavy)
{
    _isHeavyGravity = toHeavy;

    float startGravity = Gravity;
    float endGravity = toHeavy ? targetStrongGravity : _defaultGravity;

    float startMove = MoveSpeed;
    float endMove = toHeavy ? _defaultMoveSpeed * heavyMoveSpeedMultiplier : _defaultMoveSpeed;

    float startSprint = SprintSpeed;
    float endSprint = toHeavy ? _defaultSprintSpeed * heavyMoveSpeedMultiplier : _defaultSprintSpeed;

    float startJump = JumpHeight;
    float endJump = toHeavy ? _defaultJumpHeight * heavyJumpMultiplier : _defaultJumpHeight;

    float startFOV = _virtualCam ? _virtualCam.m_Lens.FieldOfView : 0f;
    float endFOV = _virtualCam ? startFOV + (toHeavy ? heavyFOVChange : -heavyFOVChange) : 0f;

    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime * gravityTransitionSpeed;

        Gravity = Mathf.Lerp(startGravity, endGravity, t);
        MoveSpeed = Mathf.Lerp(startMove, endMove, t);
        SprintSpeed = Mathf.Lerp(startSprint, endSprint, t);
        JumpHeight = Mathf.Lerp(startJump, endJump, t);

        if (_virtualCam)
            _virtualCam.m_Lens.FieldOfView = Mathf.Lerp(startFOV, endFOV, t);

        yield return null;
    }

    Gravity = endGravity;
    MoveSpeed = endMove;
    SprintSpeed = endSprint;
    JumpHeight = endJump;

    if (_virtualCam)
        _virtualCam.m_Lens.FieldOfView = endFOV;
}

#endregion

		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;


		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;
		   private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
		      private bool _hasAnimator;
		    private Animator _animator;
			public Animator animatorOverride;

			        private float _animationBlend;
	
		private PlayerInput _playerInput;
		public bool isInUI = false;
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				return _playerInput.currentControlScheme == "KeyboardMouse";
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}


		private void Start()
		{
			InitializeGravityDefaults();

			_hasAnimator = TryGetComponent(out _animator);
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
			_playerInput = GetComponent<PlayerInput>();
			AssignAnimationIDs();
			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
			if (animatorOverride != null)
{
    _animator = animatorOverride;
    _hasAnimator = true;
}
else
{
    _hasAnimator = TryGetComponent(out _animator);
}
		}
 private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }
		private void Update()
		{
			
if (Keyboard.current.gKey.wasPressedThisFrame)
    ApplyStrongGravity();

if (Keyboard.current.hKey.wasPressedThisFrame)
    RevertGravity();

			
			JumpAndGravity();
			GroundedCheck();
			Move();
			
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
			
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

	private void Move()
{
    // set target speed based on move speed, sprint speed, and if sprint is pressed
    float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

    // Smoothly interpolate animation blend towards target speed.
    if (_input.move == Vector2.zero)
{
    targetSpeed = 0.0f;
}

// Smoothly interpolate animation blend towards target speed
_animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);

    // a reference to the player's current horizontal velocity
    float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

    float speedOffset = 0.1f;
    float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

    // Smoothly accelerate or decelerate to the target speed
    if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
    {
        // Use Lerp to smoothly adjust the speed towards target speed.
        _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

        // Round speed to 3 decimal places
        _speed = Mathf.Round(_speed * 1000f) / 1000f;
    }
    else
    {
        _speed = targetSpeed;
    }

    // Normalize input direction
    Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

    // If there is move input, calculate the direction of movement
    if (_input.move != Vector2.zero)
    {
        inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
    }

    // Move the player
    _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			// Smoothly update animation parameters
			if (_hasAnimator)
			{
				// Only one place where we set _animationBlend
				if (_input.move == Vector2.zero)
				{
					// Fade out to idle
					_animationBlend = Mathf.Lerp(_animationBlend, 0, Time.deltaTime * SpeedChangeRate);
				}
				else
				{
					// Fade towards move/sprint speed
					float targetsSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
					_animationBlend = Mathf.Lerp(_animationBlend, targetsSpeed, Time.deltaTime * SpeedChangeRate);
				}


				// Apply the speed blend to the animator
				_animator.SetFloat(_animIDSpeed, _animationBlend);
				_animator.SetFloat(_animIDMotionSpeed, inputMagnitude);


    }
}



		private void JumpAndGravity()
{
    if (Grounded)
    {
        _fallTimeoutDelta = FallTimeout;
        if (_hasAnimator)
        {
            _animator.SetBool(_animIDJump, false);  // Ensure the jump animation is stopped when grounded
            _animator.SetBool(_animIDFreeFall, false);
        }

        // reset the fall timeout timer
        if (_verticalVelocity < 0.0f)
        {
            _verticalVelocity = -2f;  // Prevent falling indefinitely when grounded
        }

       if (_input.jump && _jumpTimeoutDelta <= 0.0f)
{
    // Base jump velocity
    float jumpVel = Mathf.Sqrt(JumpHeight * -2f * Gravity);

    // Add inherited horizontal velocity from asteroid
    Vector3 inheritedVelocity = Vector3.zero;
    if (stickScript != null)
    {
        inheritedVelocity = stickScript.ConsumeExternalVelocity();
    }

    // Apply jump
    _verticalVelocity = jumpVel;  // vertical
    Vector3 horizontalVel = new Vector3(inheritedVelocity.x, 0, inheritedVelocity.z);

    // Move player with horizontal inherited velocity
    _controller.Move(horizontalVel * Time.deltaTime);

    if (_hasAnimator)
    {
        _animator.SetBool(_animIDJump, true);
    }
}


        if (_jumpTimeoutDelta >= 0.0f)
        {
            _jumpTimeoutDelta -= Time.deltaTime;
        }
    }
    else
    {
        // Apply gravity when not grounded
        _verticalVelocity += Gravity * Time.deltaTime;

        // Prevent the player from falling indefinitely by setting a terminal velocity
        if (_verticalVelocity < -_terminalVelocity)
        {
            _verticalVelocity = -_terminalVelocity;
        }

        // Player is in the air, manage falling or freefall animation
        if (_hasAnimator)
        {
          //  _animator.SetBool(_animIDFreeFall, true);  // Trigger freefall animation when in the air
           // _animator.SetBool(_animIDJump, false);    // Ensure jump animation is stopped when falling
        }

        // Reset jump input
        _input.jump = false;
    }



		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
	
}
