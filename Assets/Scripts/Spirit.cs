using System.Collections;
using UnityEngine;

public class Spirit : MonoBehaviour
{
    public enum RelocateMethod
    {
        Teleport,
        Dash
    }

    private enum SpiritState
    {
        RealityFollowing,
        DreamSleeping
    }

    [Header("추적 대상")]
    public Transform player;

    [Header("현실 위치 설정")]
    public float verticalOffset = 0.5f;
    public float frontOffset = 2f;

    [Header("현실 추적 지연")]
    public float followSmoothTime = 0.2f;

    [Header("꿈 → 현실 복귀")]
    public RelocateMethod relocateMethod = RelocateMethod.Dash;
    public float dashSpeed = 15f;
    public GameObject teleportEffectPrefab;

    [Header("꿈에서 떨어지는 중력")]
    public float sleepGravityScale = 1f;

    private Animator anim;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private SpiritState state = SpiritState.RealityFollowing;

    // 현실에서 유지할 고정 Y
    private float fixedY;

    // 현실에서 X축 추적용
    private float velocityX;

    private float previousPlayerX;

    // 마지막 플레이어 이동 방향
    // 1 = 오른쪽
    // -1 = 왼쪽
    private float lastMoveDirection = 1f;

    private bool isDashing = false;
    private Coroutine dashCoroutine;

    private static readonly int IsSleepingHash =
        Animator.StringToHash("is_sleeping");

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        ResetToDesignatedPosition();
    }

    void Update()
    {
        if (player == null)
            return;

        // 현실에서만 플레이어 이동 방향을 확인하고
        // 스프라이트 좌우반전을 갱신한다.
        //
        // 꿈에서는 아예 실행하지 않기 때문에
        // 꿈에 들어간 순간의 방향이 그대로 유지된다.
        if (state == SpiritState.RealityFollowing)
        {
            UpdateMoveDirection();
            HandleRealityFollowing();
        }
        else if (state == SpiritState.DreamSleeping)
        {
            HandleDreamSleeping();
        }
    }

    // ================================================================
    // 플레이어 이동 방향
    // ================================================================

    private void UpdateMoveDirection()
    {
        float deltaX = player.position.x - previousPlayerX;

        if (deltaX > 0.0001f)
        {
            lastMoveDirection = 1f;
        }
        else if (deltaX < -0.0001f)
        {
            lastMoveDirection = -1f;
        }

        previousPlayerX = player.position.x;

        if (spriteRenderer != null)
        {
            // 위치는 바꾸지 않고 스프라이트만 반전.
            spriteRenderer.flipX = lastMoveDirection < 0f;
        }
    }

    // ================================================================
    // 현실 추적
    // ================================================================

    private void HandleRealityFollowing()
    {
        if (isDashing)
            return;

        // 항상 플레이어 오른쪽.
        // 왼쪽으로 이동해도 스피릿 위치는 여전히 오른쪽이다.
        float targetX = player.position.x + frontOffset;

        // X만 부드럽게 따라감.
        float smoothedX = Mathf.SmoothDamp(
            transform.position.x,
            targetX,
            ref velocityX,
            followSmoothTime
        );

        // Y는 절대 플레이어를 따라가지 않는다.
        // fixedY를 계속 유지한다.
        transform.position = new Vector3(
            smoothedX,
            fixedY,
            transform.position.z
        );
    }

    // ================================================================
    // 꿈
    // ================================================================

    private void HandleDreamSleeping()
    {
        // 꿈에서는 아무것도 하지 않는다.
        //
        // 플레이어 추적 X
        // X 위치 변경 X
        // 좌우반전 갱신 X
        //
        // Rigidbody2D의 중력만 적용된다.
    }

    // ================================================================
    // 현실 → 꿈
    // ================================================================

    public void EnterDream()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;

        if (anim != null)
        {
            anim.SetBool(IsSleepingHash, true);
        }

        // 꿈에 들어가는 순간 중력 ON
        if (rb != null)
        {
            rb.gravityScale = sleepGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 여기부터 꿈 상태.
        // 이후 Update에서는 현실 추적 로직이 실행되지 않는다.
        state = SpiritState.DreamSleeping;
    }

    // ================================================================
    // 꿈 → 현실
    // ================================================================

    // GameManager가 화면 전환이 끝난 뒤 호출한다.
    public void EnterReality()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;

        // 현실에서는 중력 OFF
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (anim != null)
        {
            anim.SetBool(IsSleepingHash, false);
        }

        state = SpiritState.RealityFollowing;

        // 꿈에서 떨어졌던 현재 위치에서
        // 현실의 지정 위치까지 날아온다.
        RelocateToFront();
    }

    // ================================================================
    // 현실 지정 위치
    // ================================================================

    private Vector3 GetDesignatedRealityPosition()
    {
        if (player == null)
            return transform.position;

        return new Vector3(
            player.position.x + frontOffset,
            fixedY,
            transform.position.z
        );
    }

    // ================================================================
    // 재배치
    // ================================================================

    private void RelocateToFront()
    {
        if (player == null)
            return;

        if (relocateMethod == RelocateMethod.Teleport)
        {
            Vector3 targetPosition =
                GetDesignatedRealityPosition();

            if (teleportEffectPrefab != null)
            {
                Instantiate(
                    teleportEffectPrefab,
                    transform.position,
                    Quaternion.identity
                );

                Instantiate(
                    teleportEffectPrefab,
                    targetPosition,
                    Quaternion.identity
                );
            }

            transform.position = targetPosition;

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            velocityX = 0f;
        }
        else
        {
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
            }

            dashCoroutine = StartCoroutine(
                DashToPosition()
            );
        }
    }

    private IEnumerator DashToPosition()
    {
        isDashing = true;

        while (player != null)
        {
            // 플레이어가 이동 중이면 목표 X도 계속 갱신.
            // Y는 fixedY 그대로.
            Vector3 currentTarget =
                GetDesignatedRealityPosition();

            transform.position = Vector3.MoveTowards(
                transform.position,
                currentTarget,
                dashSpeed * Time.deltaTime
            );

            if (Vector2.Distance(
                    transform.position,
                    currentTarget
                ) <= 0.05f)
            {
                transform.position = currentTarget;
                break;
            }

            yield return null;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        velocityX = 0f;
        isDashing = false;
        dashCoroutine = null;
    }

    // ================================================================
    // 초기화
    // ================================================================

    public void ResetToDesignatedPosition()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;

        if (anim != null)
        {
            anim.SetBool(IsSleepingHash, false);
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (player != null)
        {
            // verticalOffset은 여기서만 사용한다.
            // 플레이어의 현재 Y를 기준으로 fixedY를 한 번 결정한다.
            //
            // 이후 플레이어가 점프해도 fixedY는 바뀌지 않는다.
            fixedY = player.position.y + verticalOffset;

            // 현실에서는 항상 플레이어 오른쪽.
            float targetX =
                player.position.x + frontOffset;

            transform.position = new Vector3(
                targetX,
                fixedY,
                transform.position.z
            );

            previousPlayerX = player.position.x;
            lastMoveDirection = 1f;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
        }

        velocityX = 0f;
        state = SpiritState.RealityFollowing;
    }
}
