using System.Collections;
using UnityEngine;

// Main 씬에 들어가자마자 화면을 검게 깔아두고, 지정된 튜토리얼(TutorialTrigger.releasesIntroFade가
// 체크된 트리거)이 끝나는 순간 서서히 걷어내는 인트로 페이드입니다.
//
// [Unity 설정]
// 1. 화면 전체를 덮는 Canvas(검은 Image 포함) 오브젝트를 만들고 CanvasGroup을 붙이세요.
// 2. 이 스크립트를 그 오브젝트에 붙이세요.
// 3. GameManager의 Intro Fade 필드에 이 오브젝트를 연결하세요.
// 4. 페이드를 풀고 싶은 시점의 TutorialTrigger에서 Releases Intro Fade를 체크하세요.
[RequireComponent(typeof(CanvasGroup))]
public class MainSceneIntroFade : MonoBehaviour
{
    [Header("페이드 해제 시간")]
    public float fadeOutDuration = 1.5f;

    private CanvasGroup canvasGroup;
    private bool hasReleased = false; // 중복 호출(체인 중 여러 트리거가 실수로 체크된 경우 등) 방지

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        // 씬 시작과 동시에 항상 완전히 불투명한 검은 화면으로 시작 (요청 3번: 우선 페이드 인 해둠)
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        Debug.Log("[IntroFade] Awake: 화면을 alpha=1(완전 검정)로 초기화");
    }

    /// <summary>
    /// 지정된 튜토리얼이 끝났을 때 GameManager가 호출합니다. fadeOutDuration에 걸쳐 서서히 걷힙니다.
    /// 한 번 걷힌 뒤에는 다시 호출되어도 아무 일도 하지 않습니다.
    /// </summary>
    public void ReleaseFade()
    {
        if (hasReleased)
        {
            Debug.LogWarning("[IntroFade] ReleaseFade()가 이미 호출된 상태에서 다시 호출됨 — 무시합니다.");
            return;
        }
        hasReleased = true;
        Debug.Log($"[IntroFade] ReleaseFade() 호출됨 — {fadeOutDuration}초에 걸쳐 페이드 아웃 시작");
        StartCoroutine(ReleaseFadeRoutine());
    }

    private IEnumerator ReleaseFadeRoutine()
    {
        yield return FadeUtility.LerpFloat(1f, 0f, fadeOutDuration, v => canvasGroup.alpha = v);

        // 다 걷힌 뒤에는 화면 클릭/입력을 더 이상 막지 않도록 정리
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Debug.Log("[IntroFade] 페이드 아웃 완료 (alpha=0)");
    }
}