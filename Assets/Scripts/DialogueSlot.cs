using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;

// 대화(대사) 한 칸을 표시하는 슬롯. 플레이어용/정령용처럼 슬롯마다 이 컴포넌트를 하나씩 붙여서 씁니다.
// 슬롯이 몇 개든, 위치가 어디든 이 스크립트가 하는 일은 완전히 동일합니다
// (텍스트 표시/교체/스킵/끄기) — 플레이어인지 정령인지는 이 컴포넌트 입장에서 구분할 필요가 없습니다.
//
// [사용 방식 3가지]
// - 즉시 표시(페이드 없음): Main 씬의 튜토리얼 팝업처럼 바로 나타나야 할 때 → Show(text) / Hide()
// - 페이드로 표시(화자가 바뀔 때): 슬롯 전체가 나타나거나 사라져야 할 때 → Show(text, fadeDuration) / Hide(fadeDuration)
// - [추가] 페이드 없이 텍스트만 교체(같은 화자가 계속 말할 때): 슬롯은 계속 켜진 채로 텍스트만 바꿔야 할 때
//   → SwapText(text) / SwapTextRoutine(text)
//   완료를 기다려야 한다면 각각의 Routine을 직접 StartCoroutine으로 실행하세요.
//   예: yield return StartCoroutine(mySlot.ShowRoutine("대사", 0.5f));
// [추가] typingCharsPerSecond(초당 글자 수)를 넘기면, 텍스트가 한 글자씩 타이핑되듯 표시됩니다.
// 0 이하로 넘기면 텍스트가 즉시 전체 표시됩니다.
[RequireComponent(typeof(CanvasGroup))]
public class DialogueSlot : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;

    private CanvasGroup canvasGroup;
    private string currentText = null; // 현재 이 슬롯에 떠있는 텍스트 (없으면 null)
    private Coroutine fadeCoroutine;   // 진행 중인 페이드/타이핑 코루틴 참조 (중복 호출 시 정리용)

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        Hide();
    }

    /// <summary>
    /// 이 슬롯에 text를 즉시 표시합니다 (페이드 없음, 타이핑 없음).
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
    /// 슬롯 전체를 fadeDuration에 걸쳐 페이드로 표시합니다 (화자가 바뀌어 슬롯이 새로 나타날 때 사용).
    /// 완료를 기다릴 필요가 없을 때 사용하세요. (기다려야 한다면 ShowRoutine을 직접 StartCoroutine으로 실행하세요)
    /// </summary>
    public void Show(string text, float fadeDuration)
    {
        if (currentText == text) return;
        fadeCoroutine = StartCoroutine(ShowRoutine(text, fadeDuration));
    }

    /// <summary>
    /// [주의] 슬롯 전체가 이미 떠있는 상태에서 텍스트만 바뀌는 경우에도, 여기서는 슬롯을 한 번
    /// 페이드 아웃했다가 다시 페이드 인합니다. 같은 화자가 계속 말하는 중이라 슬롯 자체는 계속 켜진 채로
    /// 텍스트만 바꾸고 싶다면 이 메서드 대신 SwapTextRoutine()을 사용하세요.
    /// typingCharsPerSecond를 0보다 크게 주면, 페이드 인이 끝난 뒤 그 속도(초당 글자 수)로
    /// 텍스트가 한 글자씩 타이핑되듯 표시됩니다. 0 이하면 텍스트가 즉시 전체 표시됩니다.
    /// </summary>
    public IEnumerator ShowRoutine(string text, float fadeDuration, float typingCharsPerSecond = 0f)
    {
        if (currentText == text) yield break;

        StopActiveFade();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (canvasGroup.alpha > 0f)
        {
            yield return FadeUtility.LerpFloat(canvasGroup.alpha, 0f, fadeDuration, v => canvasGroup.alpha = v);
        }

        currentText = text;

        bool useTyping = typingCharsPerSecond > 0f && dialogueText != null;
        if (dialogueText != null) dialogueText.text = useTyping ? string.Empty : text;

        yield return FadeUtility.LerpFloat(0f, 1f, fadeDuration, v => canvasGroup.alpha = v);

        if (useTyping)
        {
            yield return TypeText(text, typingCharsPerSecond);
        }

        fadeCoroutine = null;
    }

    /// <summary>
    /// [추가] 슬롯의 alpha(배경/스프라이트)는 전혀 건드리지 않고, 텍스트만 즉시(또는 타이핑으로) 교체합니다.
    /// 같은 화자가 계속 말할 때 슬롯이 깜빡이지 않게 하려고 사용합니다.
    /// 완료를 기다릴 필요가 없을 때 사용하세요.
    /// </summary>
    public void SwapText(string text, float typingCharsPerSecond = 0f)
    {
        if (currentText == text) return;
        fadeCoroutine = StartCoroutine(SwapTextRoutine(text, typingCharsPerSecond));
    }

    /// <summary>SwapText와 동일하지만, 완료 시점까지 기다릴 수 있도록 IEnumerator를 반환합니다.</summary>
    public IEnumerator SwapTextRoutine(string text, float typingCharsPerSecond = 0f)
    {
        if (currentText == text) yield break;

        StopActiveFade();

        // [핵심] 여기서는 alpha를 절대 건드리지 않음 — 슬롯은 계속 떠있는 상태 그대로 유지
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        currentText = text;

        bool useTyping = typingCharsPerSecond > 0f && dialogueText != null;
        if (dialogueText != null) dialogueText.text = useTyping ? string.Empty : text;

        if (useTyping)
        {
            yield return TypeText(text, typingCharsPerSecond);
        }

        fadeCoroutine = null;
    }

    // text를 charsPerSecond 속도(초당 글자 수)로 한 글자씩 표시
    private IEnumerator TypeText(string text, float charsPerSecond)
    {
        float delay = 1f / charsPerSecond;
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            sb.Append(text[i]);
            dialogueText.text = sb.ToString();
            yield return new WaitForSeconds(delay);
        }
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

        yield return FadeUtility.LerpFloat(canvasGroup.alpha, 0f, fadeDuration, v => canvasGroup.alpha = v);

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