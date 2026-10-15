using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 및 점프 세팅")]
    public float moveSpeed = 5f;       // 좌우 이동 속도
    public float acceleration = 25f;   // 초당 속도 변화량. 클수록 목표 속도에 빨리 도달(더 딱딱하게), 작을수록 서서히 가속/감속
    public float jumpForce = 8f;       // 점프 힘 (높이)
    public float jumpCutAmount = 7f;   // 짧게 눌렀을 때 최대 점프에서 깎이는 양 (최소 점프 속도 = jumpForce - jumpCutAmount)
    public float originalScale = 2f;   // 캐릭터 원래 크기

    [Header("바닥 체크 세팅")]
    public Transform groundCheck;      // 바닥을 체크할 오브젝트 위치
    public LayerMask groundLayer;      // 바닥으로 인식할 레이어
    public LayerMask wallLayer;        // 벽(Wall)에 서 있어도 점프 가능하도록 인식할 레이어
    public float checkRadius = 0.2f;   // 바닥 체크 반경

    [Header("능력 잠금 (튜토리얼로 해금)")]
    public bool canWalk = false;
    public bool canJump = false;

    [HideInInspector]
    public bool canMove = true; // 컷씬/튜토리얼 진행 중 전체 이동을 잠깐 멈추는 임시 잠금 (능력 해금과는 별개)

    // 대화창/확인창/전환 연출이 진행되는 동안 GameManager가 켜는 입력 잠금.
    public static bool InputLocked = false;

    [HideInInspector]
    public bool isInDream = false;

    private Rigidbody2D rb;
    private Animator anim;
    private PlayerAudio playerAudio;
    private float moveInput;
    private bool isGrounded;
    private bool wasGrounded;

    // 점프대 등으로 발사된 상태. true인 동안은 Space를 떼도 가변 점프(속도 깎기)가 적용되지 않음.
    // 위로 올라가는 속도가 끝나는 순간(정점) 자동으로 해제됨.
    private bool isLaunched = false;

    // [추가] 점프대 발사 직후 좌우 조작을 막는 시간 제한. Time.time이 이 값보다 작으면 좌우 입력 무시.
    private float launchControlLockEndTime = -1f;
    private bool IsLaunchControlLocked => Time.time < launchControlLockEndTime;

    private static readonly int IsWalkingHash = Animator.StringToHash("is_walking");
    private static readonly int IsJumpingHash = Animator.StringToHash("is_jumping");

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        playerAudio = GetComponent<PlayerAudio>();
    }

    void Update()
    {
        // 대화 슬롯이 하나라도 떠있으면 이동/점프(Space, A/D, 방향키) 입력을 전부 무시
        bool inputAllowed = canMove && !InputLocked && !DialogueSlot.AnyShowing;

        // 1. 키보드 좌우 입력 감지
        // [수정] 점프대 발사 직후 잠금 시간 동안에는 좌우 입력을 무시 (위로만 곧게 올라감)
        moveInput = (inputAllowed && canWalk && !IsLaunchControlLocked) ? Input.GetAxisRaw("Horizontal") : 0f;

        // 꿈 세계에 있을 때는 A/D(좌우) 진행 방향을 반대로 뒤집음
        if (isInDream)
        {
            moveInput = -moveInput;
        }

        // 2. 이동 애니메이션 및 좌우 반전 처리
        bool isMoving = moveInput != 0;
        anim.SetBool(IsWalkingHash, isMoving);
        if (isMoving)
        {
            transform.localScale = new Vector3(moveInput * originalScale, originalScale, originalScale);
        }

        // 3. 바닥 체크
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer | wallLayer);
        anim.SetBool(IsJumpingHash, !isGrounded);

        // 착지 순간 감지
        if (!wasGrounded && isGrounded)
        {
            if (playerAudio != null) playerAudio.PlayLandingSound(isMoving);
        }
        wasGrounded = isGrounded;

        // 발소리 제어
        bool isWalking = isMoving && isGrounded;
        if (playerAudio != null) playerAudio.SetWalking(isWalking);

        // 4. 스페이스바 점프 입력
        if (inputAllowed && canJump && Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            anim.SetBool(IsJumpingHash, true);
        }

        // 발사 상태 해제: 위로 올라가는 속도가 끝나면(정점 도달) 다시 일반 상태로
        if (isLaunched && rb.linearVelocity.y <= 0f)
        {
            isLaunched = false;
        }

        // 가변 점프: 상승 중에 스페이스바를 떼면 위로 향하는 속도를 최소 점프 속도로 줄임
        // 점프대로 발사된 동안에는 적용하지 않음 (발사가 잘리는 것 방지)
        if (!isLaunched && Input.GetKeyUp(KeyCode.Space))
        {
            float minJumpVelocity = Mathf.Max(0f, jumpForce - jumpCutAmount);
            if (rb.linearVelocity.y > minJumpVelocity)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, minJumpVelocity);
            }
        }
    }

    void FixedUpdate()
    {
        float targetSpeedX = moveInput * moveSpeed;
        float newSpeedX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeedX, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newSpeedX, rb.linearVelocity.y);
    }

    /// <summary>
    /// 점프대(JumpPad) 등 외부에서 플레이어를 위로 쏘아 올릴 때 호출합니다.
    /// 일반 점프와 달리 canJump 잠금과 상관없이 동작하며, 발사 중에는 Space를 떼도 속도가 깎이지 않습니다.
    /// [추가] controlLockDuration(초) 동안은 좌우 조작이 막히고, 발사 순간 가로 속도도 0으로 만들어
    /// 위로 곧게 올라가게 합니다. 0 이하로 주면 기존처럼 공중에서 바로 조작할 수 있습니다.
    /// </summary>
    public void Launch(float upwardVelocity, float controlLockDuration = 0f)
    {
        if (rb == null) return;

        isLaunched = true;

        float newVelocityX = rb.linearVelocity.x;
        if (controlLockDuration > 0f)
        {
            launchControlLockEndTime = Time.time + controlLockDuration;
            newVelocityX = 0f; // 달려오던 관성이 남아 비스듬히 날아가는 것 방지
            moveInput = 0f;
        }

        rb.linearVelocity = new Vector2(newVelocityX, upwardVelocity);
        anim.SetBool(IsJumpingHash, true);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        }
    }
}