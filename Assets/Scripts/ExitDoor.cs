using System.Collections;
using UnityEngine;

// 탈출구(문)에 플레이어가 닿으면 화면이 잠깐 일렁이다가, 지정된 시간 뒤에 탈출 결과를 처리합니다.
// 지금은 콘솔에 "탈출성공!"을 출력하는 것까지만 구현되어 있고,
// 나중에 씬 전환/엔딩 UI 등으로 그대로 확장하면 됩니다 (ExitSequence() 마지막 부분만 바꾸면 됨).
//
// [Unity 설정]
// 1. 문 오브젝트에 Collider2D(BoxCollider2D 등)를 붙이고 Is Trigger 체크 (Reset()에서 자동으로 켜줌)
// 2. 이 스크립트를 문 오브젝트에 붙이기
// 3. Camera Controller 필드에 Main Camera(CameraAndBackgroundController가 붙은 오브젝트)를 연결
// 4. Player 오브젝트의 Tag가 "Player"로 되어있는지 확인
[RequireComponent(typeof(Collider2D))]
public class ExitDoor : MonoBehaviour
{
    [Header("연동")]
    public CameraAndBackgroundController cameraController; // 화면 일렁임 연출용

    [Header("탈출 연출 설정")]
    public float rippleDuration = 2f;      // 일렁임 지속 시간(초) → 이 시간 뒤에 탈출 처리
    public float rippleMagnitude = 0.15f;  // 일렁임 강도 (클수록 더 세게 흔들림)

    [Header("플레이어 인식용 태그")]
    public string playerTag = "Player";

    private bool hasTriggered = false; // 문에 여러 번 닿아도 중복 실행되지 않도록 방지

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryTriggerExit(other);
    }

    /// <summary>
    /// 실제 트리거 감지(OnTriggerEnter2D)뿐 아니라, GameManager가 순간이동/세계 전환 직후
    /// "혹시 플레이어가 이미 문과 겹쳐있는 상태로 순간이동했는지"를 수동으로 확인할 때도
    /// 이 메서드를 그대로 호출해서 재사용합니다.
    /// </summary>
    public void TryTriggerExit(Collider2D other)
    {
        if (hasTriggered) return;
        if (other == null || !other.CompareTag(playerTag)) return;

        hasTriggered = true;
        StartCoroutine(ExitSequence());
    }

    IEnumerator ExitSequence()
    {
        // 1. 화면 일렁임 시작
        if (cameraController != null)
        {
            cameraController.PlayRippleEffect(rippleDuration, rippleMagnitude);
        }

        // 2. 일렁임이 끝날 때까지 대기
        yield return new WaitForSeconds(rippleDuration);

        // 3. [TODO] 지금은 콘솔 출력만. 나중에 씬 전환/엔딩 UI 등으로 교체하면 됨
        Debug.Log("탈출성공!");
    }
}