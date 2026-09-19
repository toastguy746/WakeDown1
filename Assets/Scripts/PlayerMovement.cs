using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 및 점프 세팅")]
    public float moveSpeed = 5f;       // 좌우 이동 속도
    public float jumpForce = 8f;       // 점프 힘 (높이)
    public float originalScale = 2f;   // 캐릭터 원래 크기

    [Header("바닥 체크 세팅")]
    public Transform groundCheck;      // 바닥을 체크할 오브젝트 위치
    public LayerMask groundLayer;      // 바닥으로 인식할 레이어
    public LayerMask wallLayer;        // 벽(Wall)에 서 있어도 점프 가능하도록 인식할 레이어
    public float checkRadius = 0.2f;   // 바닥 체크 반경

    [Header("능력 잠금 (튜토리얼로 해금)")]
    // [추가] 기본적으로 잠겨있다가, GameManager가 해당 능력을 해금하는 TutorialTrigger를
    // 처리할 때 true로 바뀝니다. false인 동안은 해당 입력이 아예 무시됩니다.
    public bool canWalk = false;
    public bool canJump = false;

    [HideInInspector]
    public bool canMove = true; // [기존] 컷씬/튜토리얼 진행 중 전체 이동을 잠깐 멈추는 임시 잠금 (능력 해금과는 별개)

    [HideInInspector]
    // 플레이어가 현재 꿈 세계에 있는지 현실 세계에 있는지 추적하는 변수.
    // GameManager가 세계를 전환할 때마다(SwitchWorldSequence) 이 값을 갱신해줍니다.
    public bool isInDream = false;

    private Rigidbody2D rb;
    private Animator anim;
    // 발소리/착지음 재생은 같은 오브젝트에 붙은 PlayerAudio 컴포넌트로 위임
    private PlayerAudio playerAudio;
    private float moveInput;
    private bool isGrounded;           // 바닥에 서 있는지 여부
    private bool wasGrounded;          // 이전 프레임에 바닥이었는지 기억하는 변수

    // 문자열로 anim.SetBool/GetBool을 호출하면 매번 내부적으로 문자열 해시 변환이 일어남.
    // 자주 호출되는 파라미터는 미리 해시값을 캐싱해두면 같은 동작을 유지하면서 호출 비용을 줄일 수 있음.
    private static readonly int IsWalkingHash = Animator.StringToHash("is_walking");
    private static readonly int IsJumpingHash = Animator.StringToHash("is_jumping");

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        playerAudio = GetComponent<PlayerAudio>(); // 같은 오브젝트의 PlayerAudio 캐싱
    }

    void Update()
    {
        // 게임 매니저가 움직임을 막았으면(컷씬/튜토리얼 진행 중) 속도를 0으로 만들고 대기
        if (!canMove)
        {
            moveInput = 0;
            anim.SetBool(IsWalkingHash, false);
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            if (playerAudio != null) playerAudio.SetWalking(false); // 움직임이 막히면 발소리도 즉시 정지
            return;
        }

        // 1. 키보드 좌우 입력 감지 ([변경] canWalk가 해금되지 않았으면 입력을 아예 무시)
        moveInput = canWalk ? Input.GetAxisRaw("Horizontal") : 0f;

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

        // 3. 바닥 체크 (Wall 레이어도 함께 인식해서 벽을 밟고 있어도 점프 가능하게 함)
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer | wallLayer);
        anim.SetBool(IsJumpingHash, !isGrounded);

        // 착지 순간 감지 (이전 프레임엔 공중이었는데 지금 프레임에 바닥에 닿았을 때)
        if (!wasGrounded && isGrounded)
        {
            if (playerAudio != null) playerAudio.PlayLandingSound(isMoving);
        }

        // 현재 바닥 상태를 다음 프레임에서 기억할 수 있도록 저장
        wasGrounded = isGrounded;

        // 발소리 제어 호출 (걷고 있는 상태이면서 동시에 바닥에 붙어있을 때만 소리 나게 설정)
        bool isWalking = isMoving && isGrounded;
        if (playerAudio != null) playerAudio.SetWalking(isWalking);

        // 4. 스페이스바 점프 입력 ([변경] canJump이 해금되지 않았으면 점프 입력 무시)
        if (canJump && Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            anim.SetBool(IsJumpingHash, true);
        }
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        // 5. 물리 엔진 기반 좌우 이동 (canWalk가 꺼져있으면 moveInput이 이미 0이라 자연스럽게 정지됨)
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
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