using System.Collections;
using UnityEngine;

// 플레이어를 따라다니는 정령(펫) 컴패니언.
//
// - 현실 세계(RealityFollowing): 항상 플레이어보다 X가 큰 쪽(오른쪽)에서 부드럽게 추적,
//   Y는 진입 시점에 고정된 절대값(fixedY) 유지, 이동 방향에 따라 스프라이트만 좌우 반전.
// - 꿈 세계(DreamSleeping): 추적을 완전히 멈추고 중력을 켜서 그 자리에서 떨어짐(잠듦 연출).
// - 꿈 → 현실 복귀 시 GameManager가 EnterReality()를 호출하면, 떨어졌던 위치에서
//   현실의 지정 위치까지 Teleport 또는 Dash로 날아옴.
public class Spirit : MonoBehaviour
{
    public enum RelocateMethod { Teleport, Dash }
    private enum SpiritState { RealityFollowing, DreamSleeping }

    [Header("추적 대상")]
    public Transform player;

    [Header("현실 위치 설정")]
    public float verticalOffset = 0.5f; // 리셋 시점 플레이어 Y 기준 고정 높이
    public float frontOffset = 2f;      // 항상 플레이어보다 이만큼 오른쪽에 위치

    [Header("현실 추적 지연")]
    public float followSmoothTime = 0.2f;

    [Header("꿈 → 현실 복귀 방식")]
    public RelocateMethod relocateMethod = RelocateMethod.Dash;
    public float dashSpeed = 15f; // 목표에 가까워질수록 이 값에 수렴하는 '기준 속도'
    // [추가] 거리에 비례해서 붙는 추가 속도 배율. 멀수록 dashSpeed + 거리*이 값 만큼 더 빠르고,
    // 거리가 0에 가까워질수록 자연스럽게 dashSpeed로 수렴함
    public float distanceSpeedMultiplier = 0.5f;
    public GameObject teleportEffectPrefab;

    [Header("꿈에서 떨어지는 중력")]
    public float sleepGravityScale = 1f;

    private Animator anim;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private SpiritState state = SpiritState.RealityFollowing;

    private float fixedY;          // 현실에서 유지할 고정 Y (리셋 시점에 결정, 점프해도 안 바뀜)
    private float velocityX;       // SmoothDamp용 내부 속도값
    private float previousPlayerX;
    private float lastMoveDirection = 1f; // 1: 오른쪽, -1: 왼쪽 (스프라이트 반전용)
    private bool isDashing = false;
    private Coroutine dashCoroutine;

    private static readonly int IsSleepingHash = Animator.StringToHash("is_sleeping");

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        StopPhysicsForRealityFollow();
        ResetToDesignatedPosition();
    }

    void Update()
    {
        if (player == null) return;

        if (state == SpiritState.RealityFollowing)
        {
            // 꿈에서는 이 갱신 자체가 실행되지 않으므로, 꿈에 들어간 순간의 방향/위치가 그대로 유지됨
            UpdateMoveDirection();
            HandleRealityFollowing();
        }
        // DreamSleeping 상태에서는 Rigidbody2D 중력만 작동하고 별도 로직 없음
    }

    private void UpdateMoveDirection()
    {
        float deltaX = player.position.x - previousPlayerX;
        if (deltaX > 0.0001f) lastMoveDirection = 1f;
        else if (deltaX < -0.0001f) lastMoveDirection = -1f;
        previousPlayerX = player.position.x;

        if (spriteRenderer != null) spriteRenderer.flipX = lastMoveDirection < 0f;
    }

    private void HandleRealityFollowing()
    {
        if (isDashing) return;

        // 항상 플레이어 오른쪽(왼쪽으로 이동해도 좌우가 안 바뀜). X만 부드럽게 추적, Y는 고정.
        float targetX = player.position.x + frontOffset;
        float smoothedX = Mathf.SmoothDamp(transform.position.x, targetX, ref velocityX, followSmoothTime);
        transform.position = new Vector3(smoothedX, fixedY, transform.position.z);
    }

    // ================================================================
    // 현실 ↔ 꿈 전환
    // ================================================================

    /// <summary>현실 → 꿈. GameManager가 화면이 검은 동안 호출. 그 자리에서 중력을 켜고 떨어뜨림.</summary>
    public void EnterDream()
    {
        CancelDash();
        if (anim != null) anim.SetBool(IsSleepingHash, true);

        if (rb != null)
        {
            rb.gravityScale = sleepGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        state = SpiritState.DreamSleeping; // 이후 Update()에서 추적 로직이 실행되지 않음
    }

    /// <summary>꿈 → 현실. GameManager가 화면이 완전히 밝아진 뒤 호출. 지정 위치까지 복귀 연출 재생.</summary>
    public void EnterReality()
    {
        CancelDash();
        StopPhysicsForRealityFollow();
        if (anim != null) anim.SetBool(IsSleepingHash, false);

        state = SpiritState.RealityFollowing;
        RelocateToFront(); // 꿈에서 떨어졌던 위치 → 현실 지정 위치로 이동 시작
    }

    // ================================================================
    // 재배치 (Teleport / Dash)
    // ================================================================

    private Vector3 GetDesignatedRealityPosition()
    {
        if (player == null) return transform.position;
        return new Vector3(player.position.x + frontOffset, fixedY, transform.position.z);
    }

    private void RelocateToFront()
    {
        if (player == null) return;

        if (relocateMethod == RelocateMethod.Teleport)
        {
            Vector3 targetPosition = GetDesignatedRealityPosition();

            if (teleportEffectPrefab != null)
            {
                Instantiate(teleportEffectPrefab, transform.position, Quaternion.identity); // 사라지는 자리
                Instantiate(teleportEffectPrefab, targetPosition, Quaternion.identity);      // 나타나는 자리
            }

            transform.position = targetPosition;
            StopPhysicsForRealityFollow();
            velocityX = 0f;
        }
        else
        {
            CancelDash();
            dashCoroutine = StartCoroutine(DashToPosition());
        }
    }

    private IEnumerator DashToPosition()
    {
        isDashing = true;

        while (player != null)
        {
            // 대시 도중에도 플레이어가 계속 움직일 수 있으므로 매 프레임 목표를 다시 계산
            Vector3 currentTarget = GetDesignatedRealityPosition();

            // [수정] 거리가 멀수록 더 빠르게, 가까워질수록 dashSpeed에 수렴하는 가변 속도
            float distance = Vector2.Distance(transform.position, currentTarget);
            float currentSpeed = dashSpeed + distance * distanceSpeedMultiplier;

            transform.position = Vector3.MoveTowards(transform.position, currentTarget, currentSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, currentTarget) <= 0.05f)
            {
                transform.position = currentTarget;
                break;
            }
            yield return null;
        }

        StopPhysicsForRealityFollow();
        velocityX = 0f;
        isDashing = false;
        dashCoroutine = null;
    }

    private void CancelDash()
    {
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        dashCoroutine = null;
        isDashing = false;
    }

    // 현실 추적 상태(중력 없음, 속도 0)로 되돌리는 Rigidbody 초기화. 여러 지점에서 반복 사용되던 걸 통합.
    private void StopPhysicsForRealityFollow()
    {
        if (rb == null) return;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    // ================================================================
    // 초기화 / 세계 전환 리셋
    // ================================================================

    /// <summary>씬 시작 시, 또는 확실한 리셋이 필요할 때 호출. 플레이어 현재 Y 기준으로 fixedY를 다시 계산.</summary>
    public void ResetToDesignatedPosition()
    {
        CancelDash();
        StopPhysicsForRealityFollow();
        if (anim != null) anim.SetBool(IsSleepingHash, false);

        if (player != null)
        {
            fixedY = player.position.y + verticalOffset; // 이후 플레이어가 점프해도 fixedY는 안 바뀜
            transform.position = new Vector3(player.position.x + frontOffset, fixedY, transform.position.z);
            previousPlayerX = player.position.x;
            lastMoveDirection = 1f;
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }

        velocityX = 0f;
        state = SpiritState.RealityFollowing;
    }
}