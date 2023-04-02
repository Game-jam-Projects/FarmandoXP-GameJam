using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class SpriteRotationHandler
{
    public GameObject sprite;
    public Vector3 startRotation;
    public Vector3 targetRotation;
}

[RequireComponent(typeof(InputReference), typeof(HealthSystem))]
public class PlayerController : MonoBehaviour
{
    private static readonly int AttackParam = Animator.StringToHash("attack");
    private static readonly int IsWalkParam = Animator.StringToHash("isWalk");
    private static readonly int IsDieParam = Animator.StringToHash("isDie");
    private static readonly int TakeDamageParam = Animator.StringToHash("takeDamage");
    private static readonly int DashParam = Animator.StringToHash("dash");
    private static readonly int DirectionXParam = Animator.StringToHash("directionX");
    private static readonly int DirectionYParam = Animator.StringToHash("directionY");

    public event Action<float, float> OnTriggerShootEvent; //current / max

    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float dashForce = 5f;
    [SerializeField] private float dashDelay = 1f;

    [Header("Bullets")]
    public BulletController bulletPrefab;

    [SerializeField] private Transform gunPivot;

    [Header("Gun Pivots")]
    [SerializeField] private Transform gunDownLocation;
    [SerializeField] private Transform gunUpLocation;
    [SerializeField] private Transform gunSideLocation;

    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float waitForAnimation = 0.2f;
    [SerializeField] private int maxBullets = 10;
    [SerializeField] public float delayToReload = 0.2f;

    [SerializeField] private int currentBullets = 0;
    [SerializeField] private float removingBulletsDelay = 0.5f;

    private bool _isCanShoot = true;
    private bool _isDashing = false;
    private bool _isTriggeredMovementAudio = false;
    
    private Vector2 _targetDirection;

    private float _timeStopped;
    private float _timeRemovingBullets;

    private InputReference _inputReference;
    private Rigidbody2D _rigidbody2D;

    private IDamageable _health;

    private void Awake()
    {
        _inputReference = GetComponent<InputReference>();
        _rigidbody2D = GetComponent<Rigidbody2D>();

        _health = GetComponent<IDamageable>();
    }

    private void Start()
    {
        currentBullets = maxBullets;
        
        UpdateToSide();
    }

    private void OnEnable()
    {
        _health.OnTakeDamage += ChangeToTakeDamageAnimation;
        _health.OnDie += ChangeToDieAnimation;
    }

    private void OnDisable()
    {
        _health.OnTakeDamage -= ChangeToTakeDamageAnimation;
        _health.OnDie -= ChangeToDieAnimation;
    }    

    private void Update()
    {
        if (_health.IsDie)
            return;

        PauseInputTrigger();

        if (GameManager.Instance && GameManager.Instance.Paused)
            return;

        _targetDirection = _inputReference.Movement;

        if (_targetDirection != Vector2.zero && !_isTriggeredMovementAudio)
        {
            _timeStopped = 0f;
            _isTriggeredMovementAudio = true;
            TriggerAudio();
        }

        if (_targetDirection == Vector2.zero)
        {
            _timeStopped += Time.deltaTime;

            if(_timeStopped > 0.2f)
                _isTriggeredMovementAudio = false;
        }

        ShootInputTrigger();

        UpdateAnimator();


        if (!IsMoving())
            return;

        DashInpuTrigger();

        UpdateGunRotation();
        UpdatePlayerScale();
    }

    private void TriggerAudio()
    {
        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Fada/Wings", transform.position);
    }

    private void FixedUpdate()
    {
        if (_health.IsDie)
            return;

        if(_isDashing)
            return;

        _rigidbody2D.velocity = _targetDirection * moveSpeed;
    }

    private bool IsMoving() => _targetDirection != Vector2.zero;

    private void UpdatePlayerScale()
    {
        transform.localScale = new Vector3(Mathf.Sign(_targetDirection.x), 1, 1);
    }

    public void UpdateToDown() => UpdateGunPosition(gunDownLocation.position);
    public void UpdateToUp() => UpdateGunPosition(gunUpLocation.position);
    public void UpdateToSide() => UpdateGunPosition(gunSideLocation.position);

    public bool HasBullets() => currentBullets > 0;

    public void UpdateGunPosition(Vector3 newPosition)
    {
        gunPivot.position = newPosition;
    }

    private void UpdateGunRotation()
    {
        gunPivot.up = _targetDirection;
    }

    private void PauseInputTrigger()
    {
        if (_inputReference.PauseButton.IsPressed)
        {
            GameManager.Instance.PauseResume();
        }
    }
    private void ShootInputTrigger()
    {
        if (_inputReference.ShootButton.IsPressed && currentBullets > 0)
        {
            if (!_isCanShoot)
                return;

            StartCoroutine(IE_CanShoot());

            playerAnimator.SetTrigger(AttackParam);
        }
    }
    private void DashInpuTrigger()
    {
        if(_inputReference.DashButton.IsPressed && !_isDashing)
        {
            _isDashing = true;

            _rigidbody2D.AddForce(_targetDirection * dashForce, ForceMode2D.Impulse);

            StartCoroutine(IE_ResetDash());

            playerAnimator.SetTrigger(DashParam);
        }
    }

    private IEnumerator IE_ResetDash()
    {
        yield return new WaitForSeconds(dashDelay);
        _isDashing = false;
    }

    private IEnumerator IE_CanShoot()
    {
        _isCanShoot = false;

        yield return new WaitForSeconds(waitForAnimation);

        currentBullets--;

        BulletController bullet = Instantiate(bulletPrefab, gunPivot.position, gunPivot.rotation);
        bullet.speedBullet = bulletSpeed;
        bullet.transform.up = gunPivot.up;

        OnTriggerShootEvent?.Invoke(currentBullets, maxBullets);

        yield return new WaitForSeconds(delayToReload);

        _isCanShoot = true;
    }

    public void AddBullets(int count)
    {
        if (currentBullets >= maxBullets)
            return;

        currentBullets += count;

        if(currentBullets >= maxBullets)
        {
            currentBullets = maxBullets;
        }
    }

    public void RemoveBullets()
    {
        if(currentBullets <= 0)
            return;

        _timeRemovingBullets += Time.deltaTime;

        if (_timeRemovingBullets > removingBulletsDelay)
        {
            currentBullets--;
            _timeRemovingBullets = 0;
        }
    }

    private void ChangeToDieAnimation(IDamageable value)
    {
        playerAnimator.SetBool(IsDieParam, true);
    }

    private void ChangeToTakeDamageAnimation(Vector3 value)
    {
        playerAnimator.SetTrigger(TakeDamageParam);
    }

    private void UpdateAnimator()
    {
        playerAnimator.SetBool(IsWalkParam, _rigidbody2D.velocity != new Vector2(0, 0));

        if (!IsMoving())
            return;

        playerAnimator.SetFloat(DirectionXParam, _targetDirection.x);
        playerAnimator.SetFloat(DirectionYParam, _targetDirection.y);
    }

   
}
