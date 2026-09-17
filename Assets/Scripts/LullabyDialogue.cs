using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal; // Light2D 사용을 위해 필요
using UnityEngine.SceneManagement;     // 씬 전환을 위해 필요

// 엄마와 플레이어(아이)가 번갈아 말하는 자장가 대화를 진행시키는 스크립트.
// [변경] 대사 한 줄마다 화자(Speaker)를 지정해서, 화자에 맞는 DialogueSlot만 보이고
// 다른 화자의 슬롯(스프라이트 포함)은 자동으로 숨겨지도록 처리합니다.
//
// [흐름]
// 1. 씬 시작 후 startDelay만큼 기다렸다가 첫 줄이 페이드 인으로 등장
// 2. Space(또는 자동 진행)로 다음 줄 진행:
//    - 이전 줄과 화자가 같으면 같은 슬롯 안에서 텍스트만 교체 (기존과 동일)
//    - 이전 줄과 화자가 다르면 이전 화자의 슬롯 전체(배경+스프라이트+텍스트)를 페이드 아웃한 뒤
//      새 화자의 슬롯을 페이드 인
// 3. 마지막 줄에서 한 번 더 진행하면, 마지막으로 보이던 슬롯을 페이드 아웃시키며 대사 종료 처리
//    (스팟 라이트를 0으로 서서히 낮추고, 지정한 이미지도 함께 투명화하고, 지정한 오디오를 재생)
// 4. 오디오 재생 후 musicPlayDuration만큼 지나면 음악을 페이드아웃하며 nextSceneName 씬으로 전환
//
// [Unity 설정]
// 1. 플레이어 대사창(배경+스프라이트+자신의 Text TMP)에 CanvasGroup + DialogueSlot을 붙이고,
//    Player Dialogue Slot 필드에 연결하세요.
// 2. 엄마 대사창(배경+자신의 Text TMP)에도 CanvasGroup + DialogueSlot을 붙이고,
//    Mom Dialogue Slot 필드에 연결하세요. (두 슬롯의 Text는 서로 다른 오브젝트여야 합니다)
// 3. Lines 배열에 각 줄마다 Speaker(Player/Mom)와 대사 내용을 채우세요.
// 4. 나머지(아이콘 버튼, 스팟 라이트, 오디오, 씬 전환 등) 설정은 기존과 동일합니다.
public class LullabyDialogue : MonoBehaviour
{
    public enum Speaker { Player, Mom }

    [System.Serializable]
    public class DialogueLine
    {
        public Speaker speaker;
        [TextArea(2, 4)]
        public string text;
    }

    [Header("연동 (화자별 슬롯)")]
    public DialogueSlot playerDialogueSlot; // 배경+스프라이트+텍스트를 포함하는 플레이어 쪽 슬롯
    public DialogueSlot momDialogueSlot;    // 배경+텍스트만 포함하는 엄마 쪽 슬롯

    [Header("대본 (위에서부터 순서대로, 줄마다 화자 지정)")]
    public DialogueLine[] lines = new DialogueLine[]
    {
        new DialogueLine { speaker = Speaker.Player, text = "엄마.. 안주무세요?" },
    };

    [Header("타이밍 설정")]
    public float startDelay = 2f;        // 첫 대사가 뜨기까지 대기 시간 (초기 페이드 시간과 맞추세요)
    public float lineFadeDuration = 0.5f; // 각 줄/슬롯이 나타나고 사라질 때 페이드 시간

    [Header("입력 설정")]
    public KeyCode advanceKey = KeyCode.Space;
    public Button autoAdvanceIconButton; // 오른쪽 위 아이콘 버튼 (클릭 시 자동 진행 모드 시작)
    public float autoAdvanceDelay = 2f;  // 자동 진행 모드에서 줄이 넘어가는 간격

    [Header("대사 종료 연출")]
    public Light2D spotLight;                 // 마지막 줄이 끝나면 0으로 낮출 스팟 라이트
    public ep1Manager sceneManager;           // 라이트 왕복 코루틴을 멈추기 위해 필요
    public float endingLightFadeDuration = 2f;
    public AudioSource backgroundMusicSource; // 자장가가 시작되면 즉시 꺼야 할 기존 배경음악
    public AudioSource endingAudioSource;     // 원하는 사운드(자장가)를 자유롭게 연결해서 재생

    [Header("자장가 시작 시 함께 투명화할 이미지 (선택)")]
    public CanvasGroup imageToFadeOut;
    public float imageFadeOutDuration = 2f;

    [Header("엔딩 이후 씬 전환 (선택)")]
    public float musicPlayDuration = 30f;
    public float musicFadeOutDuration = 2f;
    public string nextSceneName = "Main";

    private int currentLineIndex = -1;
    private bool isTransitioning = false; // 페이드 도중 중복 입력 방지
    private bool isFinished = false;
    private bool isAutoAdvancing = false;
    private DialogueSlot activeSlot; // [추가] 현재 화면에 떠있는 슬롯 (화자 전환 시 이걸 먼저 숨김)

    void Start()
    {
        if (sceneManager == null)
        {
            sceneManager = GetComponent<ep1Manager>();
        }

        if (autoAdvanceIconButton != null)
        {
            autoAdvanceIconButton.onClick.AddListener(OnAutoIconClicked);
        }

        StartCoroutine(WaitThenShowFirstLine());
    }

    void Update()
    {
        if (Input.GetKeyDown(advanceKey))
        {
            AdvanceLine();
        }
    }

    IEnumerator WaitThenShowFirstLine()
    {
        yield return new WaitForSeconds(startDelay); // 초기 페이드(어둠이 걷히는 연출)가 끝날 때까지 대기
        AdvanceLine();
    }

    public void OnAutoIconClicked()
    {
        if (isAutoAdvancing || isFinished) return;
        isAutoAdvancing = true;
        StartCoroutine(AutoAdvanceLoop());
    }

    IEnumerator AutoAdvanceLoop()
    {
        AdvanceLine();
        while (!isFinished)
        {
            yield return new WaitForSeconds(autoAdvanceDelay);
            AdvanceLine();
        }
        isAutoAdvancing = false;
    }

    void AdvanceLine()
    {
        if (isTransitioning || isFinished) return;

        currentLineIndex++;

        if (currentLineIndex >= lines.Length)
        {
            isFinished = true;
            StartCoroutine(EndDialogueSequence());
            return;
        }

        StartCoroutine(TransitionToLine(lines[currentLineIndex]));
    }

    // [변경] 화자에 맞는 슬롯을 찾아서, 화자가 바뀌었다면 이전 슬롯을 먼저 숨기고 새 슬롯을 보여줌
    IEnumerator TransitionToLine(DialogueLine line)
    {
        isTransitioning = true;

        DialogueSlot targetSlot = GetSlotForSpeaker(line.speaker);

        if (targetSlot != null)
        {
            // 화자가 바뀌었다면(이전에 떠있던 슬롯과 다르면) 이전 슬롯을 통째로 페이드 아웃
            if (activeSlot != null && activeSlot != targetSlot)
            {
                yield return StartCoroutine(activeSlot.HideRoutine(lineFadeDuration));
            }

            // 같은 화자가 계속 말하는 중이면 ShowRoutine이 알아서 텍스트만 교체하며 페이드함
            yield return StartCoroutine(targetSlot.ShowRoutine(line.text, lineFadeDuration));
            activeSlot = targetSlot;
        }

        isTransitioning = false;
    }

    private DialogueSlot GetSlotForSpeaker(Speaker speaker)
    {
        return speaker == Speaker.Player ? playerDialogueSlot : momDialogueSlot;
    }

    // 마지막으로 떠있던 슬롯을 페이드 아웃시키고, 스팟 라이트를 0으로 낮춘 뒤 지정된 사운드(자장가) 재생.
    // 지정 이미지가 있다면 함께 투명화하고, 오디오 재생 후 일정 시간 뒤 씬 전환까지 처리
    IEnumerator EndDialogueSequence()
    {
        if (activeSlot != null)
        {
            yield return StartCoroutine(activeSlot.HideRoutine(lineFadeDuration));
            activeSlot = null;
        }

        if (sceneManager != null)
        {
            sceneManager.StopIntensityControl();
        }

        if (spotLight != null)
        {
            float startIntensity = spotLight.intensity;
            float elapsed = 0f;
            while (elapsed < endingLightFadeDuration)
            {
                elapsed += Time.deltaTime;
                spotLight.intensity = Mathf.Lerp(startIntensity, 0f, elapsed / endingLightFadeDuration);
                yield return null;
            }
            spotLight.intensity = 0f;
        }

        if (imageToFadeOut != null)
        {
            yield return FadeCanvasGroup(imageToFadeOut, imageToFadeOut.alpha, 0f, imageFadeOutDuration);
        }

        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
        }

        if (endingAudioSource != null)
        {
            endingAudioSource.Play();
            StartCoroutine(FadeOutMusicThenLoadScene());
        }
    }

    IEnumerator FadeOutMusicThenLoadScene()
    {
        yield return new WaitForSeconds(musicPlayDuration);

        if (endingAudioSource != null)
        {
            float startVolume = endingAudioSource.volume;
            float elapsed = 0f;
            while (elapsed < musicFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                endingAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeOutDuration);
                yield return null;
            }
            endingAudioSource.volume = 0f;
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }
}