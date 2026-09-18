using System.Collections;
using UnityEngine;
using TMPro;

// 대화(대사) 한 칸을 표시하는 슬롯. 플레이어용/정령용처럼 슬롯마다 이 컴포넌트를 하나씩 붙여서 씁니다.
// 슬롯이 몇 개든, 위치가 어디든 이 스크립트가 하는 일은 완전히 동일합니다
// (텍스트 표시/교체/스킵/끄기) — 플레이어인지 정령인지는 이 컴포넌트 입장에서 구분할 필요가 없습니다.
//
// [사용 방식 2가지]
// - 즉시 표시(페이드 없음): Main 씬의 튜토리얼 팝업처럼 바로 나타나야 할 때 → Show(text) / Hide()
// - 페이드로 표시: Ep.1 Story의 자장가 대사처럼 부드럽게 전환되어야 할 때 → Show(text, fadeDuration) / Hide(fadeDuration)
//   완료 시점까지 기다렸다가 다음 로직을 이어가야 한다면 ShowRoutine()/HideRoutine()을 직접 StartCoroutine으로 실행하세요.
//   예: yield return StartCoroutine(mySlot.ShowRoutine("대사", 0.5f));
[RequireComponent(typeof(CanvasGroup))]
public class DialogueSlot : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;

    private CanvasGroup canvasGroup;
    private string currentText = null; // 현재 이 슬롯에 떠있는 텍스트 (없으면 null)
    private Coroutine fadeCoroutine;   // 진행 중인 페이드 코루틴 참조 (중복 호출 시 정리용)

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        Hide();
    }

    /// <summary>
    /// 이 슬롯에 text를 즉시 표시합니다 (페이드 없음).
    /// - 이미 똑같은 텍스트가 떠있다면 아무것도 하지 않고 그대로 스킵합니다.
    /// - 다른 텍스트가 떠있었다면 그 자리에서 바로 새 텍스트로 교체합니다.
    /// </summary>
    public void Show(string text)
    {
        if (currentText == text) return; // 같은 내용이면 교체 과정 스킵

        StopActiveFade();

        currentText = text;
        if (dialogueText != null) dialogueText.text = text;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// text를 fadeDuration에 걸쳐 페이드로 표시합니다. 완료를 기다릴 필요가 없을 때 사용하세요.
    /// (기다려야 한다면 ShowRoutine을 직접 StartCoroutine으로 실행하세요)
    /// </summary>
    public void Show(string text, float fadeDuration)
    {
        if (currentText == text) return;
        fadeCoroutine = StartCoroutine(ShowRoutine(text, fadeDuration));
    }

    /// <summary>
    /// 이미 다른 텍스트가 떠있었다면 fadeDuration 동안 먼저 페이드 아웃 → 텍스트 교체 →
    /// 다시 fadeDuration 동안 페이드 인. 아무것도 안 떠있었다면 페이드 인만 실행합니다.
    /// </summary>
    public IEnumerator ShowRoutine(string text, float fadeDuration)
    {
        if (currentText == text) yield break;

        StopActiveFade();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (canvasGroup.alpha > 0f)
        {
            yield return FadeUtility.LerpFloat(canvasGroup.alpha, 0f, fadeDuration, v => canvasGroup.alpha = v); // [최적화]
        }

        currentText = text;
        if (dialogueText != null) dialogueText.text = text;

        yield return FadeUtility.LerpFloat(0f, 1f, fadeDuration, v => canvasGroup.alpha = v); // [최적화]

        fadeCoroutine = null;
    }

    /// <summary>이 슬롯을 즉시 끕니다 (페이드 없음).</summary>
    public void Hide()
    {
        StopActiveFade();

        currentText = null;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>fadeDuration에 걸쳐 페이드 아웃하며 끕니다. 완료를 기다릴 필요가 없을 때 사용하세요.</summary>
    public void Hide(float fadeDuration)
    {
        fadeCoroutine = StartCoroutine(HideRoutine(fadeDuration));
    }

    /// <summary>Hide(fadeDuration)와 동일하지만, 완료 시점까지 기다릴 수 있도록 IEnumerator를 반환합니다.</summary>
    public IEnumerator HideRoutine(float fadeDuration)
    {
        StopActiveFade();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        yield return FadeUtility.LerpFloat(canvasGroup.alpha, 0f, fadeDuration, v => canvasGroup.alpha = v); // [최적화]

        currentText = null;
        fadeCoroutine = null;
    }

    private void StopActiveFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }
}