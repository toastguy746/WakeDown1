using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 카메라의 X축 추적과, 그 카메라를 따라다녀야 하는 배경들의 이동을 "한 스크립트, 한 프레임" 안에서
// 함께 처리하는 컨트롤러입니다. Script Execution Order 설정에 의존하지 않고 코드 순서로
// "카메라 이동 → 배경 이동"을 확정 지어서, 배경이 카메라를 프레임 오차 없이 따라붙게 만듭니다.
//
// [설계 요약]
// - 카메라: 플레이어의 X만 부드럽게 추적. Y는 고정값(cameraFixedY)을 그대로 유지.
//   (꿈/현실 전환 시에는 GameManager가 SetCameraFixedY()를 호출해서 Y만 갱신)
// - 배경: 실제 크기를 거대하게 늘리는 대신, 카메라를 그대로 따라다니게 해서
//   "화면 가장자리에 검은 여백이 절대 노출되지 않는 원경" 효과를 냄.
//   배경 자체 크기는 화면을 덮을 정도면 충분하므로 옆 방의 지형 공간을 침범하지 않음.
// - 예외 배경: followingBackgrounds 리스트에 넣지 않으면 카메라를 따라가지 않고 제자리에 고정됨.
// - 순간이동 대응: SnapCameraX()를 순간이동 스크립트에서 호출하면 카메라가 스무딩 없이 즉시 이동하고,
//   배경도 같은 프레임에 같이 스냅되어 빈 공간이 노출될 틈이 없음.
public class CameraAndBackgroundController : MonoBehaviour
{
    [Header("카메라 추적 대상")]
    public Transform player; // 카메라가 X축으로 추적할 대상 (플레이어)

    [Header("카메라 추적 속도")]
    public float smoothTime = 0.15f; // 값이 작을수록 더 빠르게(딱딱하게) 따라붙음

    [Header("카메라 X 오프셋")]
    // [추가] 플레이어를 화면 정중앙이 아니라 살짝 왼쪽에 두고 싶을 때 사용.
    // 값을 양수로 주면 카메라 중심이 플레이어보다 오른쪽으로 이동해서, 결과적으로
    // 화면상에는 플레이어가 왼쪽으로 치우쳐 보이고 오른쪽(진행 방향) 시야가 더 넓게 보입니다.
    // 반대로 음수를 주면 플레이어가 오른쪽으로 치우쳐 보입니다.
    public float horizontalOffset = 2f;

    [Header("카메라를 따라다닐 배경들 (최대 3종류 정도 등록)")]
    // [핵심] 여기에 등록된 배경들은 카메라의 X 좌표를 오차 없이 그대로 따라감
    public List<Transform> followingBackgrounds = new List<Transform>();

    [Header("예외 배경 (카메라를 따라가지 않고 특정 구역에 고정)")]
    // 이 배경은 followingBackgrounds에 절대 넣지 마세요. 여기 넣어두는 건 참고/문서화 목적일 뿐,
    // 실제로 이 스크립트가 손대지 않기 때문에 항상 원래 배치한 위치에 그대로 남아있습니다.
    public Transform fixedBackground;

    private float cameraVelocityX;   // SmoothDamp용 내부 속도값
    private float cameraFixedY;      // 현재 고정된 카메라 Y좌표 (GameManager가 세계 전환 시 갱신)
    private float[] backgroundOffsets; // 각 배경이 카메라와 유지해야 할 상대적 X 오프셋

    // [추가] 돔방처럼 "카메라가 플레이어를 절대 따라오면 안 되는 구역"에 들어갔을 때 false로 전환됨
    private bool isFollowEnabled = true;

    // [추가] "흔들림 효과 적용 전의 진짜 카메라 위치"와 "흔들림으로 인한 임시 오프셋"을 분리해서 관리.
    // 이렇게 나눠야 돔방처럼 카메라가 멈춰있는 상태에서도 흔들림이 매 프레임 누적되어 밀리는 일이 없음.
    private Vector3 basePosition;
    private Vector3 shakeOffset = Vector3.zero;

    void Start()
    {
        // 시작 시점의 카메라 Y를 그대로 "고정 Y"로 사용
        cameraFixedY = transform.position.y;
        basePosition = transform.position;

        // 각 배경이 처음에 배치된 위치를 기준으로, 카메라와의 상대적 X 거리를 미리 계산해둠
        // → 이후 카메라가 움직여도 이 '거리'만 그대로 유지하면 항상 같은 상대 위치를 지키게 됨
        backgroundOffsets = new float[followingBackgrounds.Count];
        for (int i = 0; i < followingBackgrounds.Count; i++)
        {
            if (followingBackgrounds[i] != null)
            {
                backgroundOffsets[i] = followingBackgrounds[i].position.x - transform.position.x;
            }
        }
    }

    void LateUpdate()
    {
        if (player == null) return;

        // [추가] 돔방처럼 추적이 꺼진 구역에서는 basePosition을 전혀 건드리지 않음 (고정된 그대로 유지)
        if (isFollowEnabled)
        {
            // 1. 카메라: X만 부드럽게 추적, Y는 고정값 유지 (흔들림 없는 '진짜' 위치 기준)
            float smoothedX = Mathf.SmoothDamp(basePosition.x, player.position.x, ref cameraVelocityX, smoothTime);
            basePosition = new Vector3(smoothedX, cameraFixedY, basePosition.z);
        }

        // 2. 실제 카메라 위치 = 진짜 위치 + 흔들림 오프셋 (평소엔 shakeOffset이 0이라 그대로 basePosition)
        transform.position = basePosition + shakeOffset;

        // 3. 배경: 카메라가 "방금" 이동한(흔들림 포함) 이 프레임 안에서 곧바로 같이 이동시켜서 프레임 오차를 없앰
        MoveFollowingBackgrounds();
    }

    private void MoveFollowingBackgrounds()
    {
        for (int i = 0; i < followingBackgrounds.Count; i++)
        {
            Transform bg = followingBackgrounds[i];
            if (bg == null) continue;

            Vector3 pos = bg.position;
            pos.x = transform.position.x + backgroundOffsets[i]; // 처음 오프셋을 그대로 유지한 채 따라붙음
            bg.position = pos;
        }
        // fixedBackground는 여기서 아무 것도 하지 않으므로 항상 원래 위치에 고정됩니다.
    }

    /// <summary>
    /// 꿈/현실 세계 전환 시 GameManager가 호출해서 카메라의 고정 Y값을 갱신합니다.
    /// (X는 건드리지 않으므로 플레이어 추적 흐름이 끊기지 않습니다)
    /// </summary>
    public void SetCameraFixedY(float y)
    {
        cameraFixedY = y;
    }

    /// <summary>
    /// 순간이동(Teleport) 기능에서 호출하세요. 스무딩 없이 카메라를 즉시 목표 X로 스냅시키고,
    /// 등록된 배경들도 같은 프레임에 같이 스냅되므로 빈 공간이 노출되지 않습니다.
    /// </summary>
    public void SnapCameraX(float targetX)
    {
        cameraVelocityX = 0f; // 다음 SmoothDamp가 이전 관성 없이 시작하도록 속도 초기화
        basePosition = new Vector3(targetX, cameraFixedY, basePosition.z);
        transform.position = basePosition + shakeOffset;
        MoveFollowingBackgrounds();
    }

    /// <summary>
    /// 돔방처럼 "카메라가 플레이어를 절대 따라오면 안 되는 구역"에 들어갔을 때 호출하세요.
    /// 카메라 추적을 끄고, 그 구역용으로 미리 정해둔 고정 위치로 즉시 스냅시킵니다.
    /// </summary>
    public void EnterFixedCameraZone(Vector3 fixedCameraPosition)
    {
        isFollowEnabled = false;
        cameraVelocityX = 0f;
        basePosition = new Vector3(fixedCameraPosition.x, fixedCameraPosition.y, basePosition.z);
        transform.position = basePosition + shakeOffset;
        MoveFollowingBackgrounds();
    }

    /// <summary>
    /// 카메라 고정 구역(돔방 등)을 벗어났을 때 호출하세요. 다시 플레이어를 추적하기 시작합니다.
    /// </summary>
    public void ExitFixedCameraZone()
    {
        isFollowEnabled = true;
        cameraVelocityX = 0f; // 재진입 시 이전 관성 없이 자연스럽게 다시 따라붙기 시작
    }

    /// <summary>
    /// 탈출구 연출 등에서 호출하는 화면 흔들림(일렁임) 효과. duration 동안 진폭이 점점 줄어드는
    /// 사인파 형태로 카메라를 흔들고, 끝나면 자동으로 원래 위치로 돌아옵니다.
    /// </summary>
    public void PlayRippleEffect(float duration, float magnitude = 0.15f, float frequency = 20f)
    {
        if (rippleCoroutine != null) StopCoroutine(rippleCoroutine);
        rippleCoroutine = StartCoroutine(RippleCoroutine(duration, magnitude, frequency));
    }

    // 흔들림 코루틴 참조 (연속으로 여러 번 트리거돼도 이전 흔들림을 정리하고 새로 시작하기 위함)
    private Coroutine rippleCoroutine;

    private IEnumerator RippleCoroutine(float duration, float magnitude, float frequency)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // 시간이 지날수록 흔들림 진폭이 점점 줄어들도록(damper) 처리
            float damper = 1f - Mathf.Clamp01(elapsed / duration);
            float offsetX = Mathf.Sin(elapsed * frequency) * magnitude * damper;
            float offsetY = Mathf.Sin(elapsed * frequency * 1.3f + 1f) * magnitude * damper; // 위상을 살짝 다르게 줘서 더 입체적인 일렁임

            shakeOffset = new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        shakeOffset = Vector3.zero;
    }
}