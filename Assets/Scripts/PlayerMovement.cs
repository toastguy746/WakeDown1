using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 및 점프 세팅")]
    public float moveSpeed = 5f;       // 좌우 이동 속도
    public float jumpForce = 8f;       // 점프 힘 (높이)
    public float originalScale = 2f;   // 캐릭터 원래 크기

    [Header("바닥 체크 세팅")]
    public Transform groundCheck;      // 바닥을 체크할 오브젝트 위치
    public LayerMask groundLayer;      // 바닥으로 인식할 레이어
    public float checkRadius = 0.2f;   // 바닥 체크 반경

    [HideInInspector]
    public bool canMove = true; // 게임 매니저가 제어할 움직임 가능 여부 변수


    private Rigidbody2D rb;
    private Animator anim;
    private AudioSource audioSource;   // 오디오 컴포넌트 변수
    private float moveInput;
    private bool isGrounded;           // 바닥에 서 있는지 여부
    private bool wasGrounded;          // [추가] 이전 프레임에 바닥이었는지 기억하는 변수

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>(); // 오디오 소스 가져오기
    }

    void Update()
    {
        // 게임 매니저가 움직임을 막았으면 속도를 0으로 만들고 대기
        if (!canMove)
        {
            moveInput = 0;
            anim.SetBool("is_walking", false);
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            HandleFootsteps(false); // 움직임이 막히면 발소리도 즉시 정지
            return;
        }

        // 1. 키보드 좌우 입력 감지
        moveInput = Input.GetAxisRaw("Horizontal");

        // 2. 이동 애니메이션 및 좌우 반전 처리
        if (moveInput != 0)
        {
            anim.SetBool("is_walking", true);
            transform.localScale = new Vector3(moveInput * originalScale, originalScale, originalScale);
        }
        else
        {
            anim.SetBool("is_walking", false);
        }

        // 3. 바닥 체크
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);

        if (isGrounded) anim.SetBool("is_jumping", false);
        else anim.SetBool("is_jumping", true);

        // [추가] 착지 순간 감지 (이전 프레임엔 공중이었는데 지금 프레임에 바닥에 닿았을 때)
        if (!wasGrounded && isGrounded)
        {
            PlayLandingSound();
        }
        
        // 현재 바닥 상태를 다음 프레임에서 기억할 수 있도록 저장
        wasGrounded = isGrounded;

        // 발소리 제어 호출 (걷고 있는 상태이면서 동시에 바닥에 붙어있을 때만 소리 나게 설정)
        bool isWalking = anim.GetBool("is_walking") && isGrounded;
        HandleFootsteps(isWalking);

        // 4. 스페이스바 점프 입력
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            anim.SetBool("is_jumping", true);
        }
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        // 5. 물리 엔진 기반 좌우 이동
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    // 무한 루프 발소리 재생/정지 함수
    void HandleFootsteps(bool isWalking)
    {
        if (audioSource == null) return; // 오디오 소스가 없을 때 에러 방지

        if (isWalking)
        {
            // 걷고 있는데 소리가 안 나고 있다면 재생 시작
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            // 멈췄거나 공중에 떴는데 소리가 나고 있다면 정지
            // 단, 착지 소리가 순간적으로 재생 중일 때는 걷기 정지 명령으로 인해 사운드가 끊기지 않게 예외 처리
            if (audioSource.isPlaying && audioSource.loop)
            {
                audioSource.Stop();
            }
        }
    }

    // [추가] 착지 소리 한 번 출력하는 함수
    void PlayLandingSound()
    {
        if (audioSource == null) return;

        // 현재 루프 소리가 재생 중이었다면 잠시 멈추고 
        audioSource.Stop();
        
        // 오디오 소스의 loop 성질을 잠시 끄고 한 번만 '쿵' 재생 (발소리와 같은 사운드 사용)
        audioSource.loop = false;
        audioSource.Play();

        // 걷는 도중에 착지했을 때 소리가 완전히 멈추지 않도록, 
        // 걷기 상태 상태라면 다음 프레임에서 다시 루프 재생이 자연스럽게 켜집니다.
        if (anim.GetBool("is_walking"))
        {
            audioSource.loop = true;
        }
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