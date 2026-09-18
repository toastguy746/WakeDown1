using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal; // Light2D 사용을 위해 필요
using UnityEngine.SceneManagement;     // 씬 전환을 위해 필요

// 엄마와 플레이어(아이)가 번갈아 말하는 자장가 대화를 진행시키는 스크립트.
// 대사 한 줄마다 화자(Speaker)를 지정해서, 화자에 맞는 DialogueSlot만 보이고
// 다른 화자의 슬롯(스프라이트 포함)은 자동으로 숨겨지도록 처리합니다.
//
// [흐름]
// 1. 씬 시작 후 startDelay만큼 기다렸다가 첫 줄이 페이드 인으로 등장
// 2. Space(또는 자동 진행)로 다음 줄 진행 (마지막 줄 바로 앞줄까지만):
//    - 이전 줄과 화자가 같으면 슬롯은 그대로 켜진 채 텍스트만 교체 (깜빡임 없음)
//    - 이전 줄과 화자가 다르면 이전 화자의 슬롯 전체를 페이드 아웃한 뒤 새 화자의 슬롯을 페이드 인
//    - 각 줄은 textTypingSpeed 속도로 한 글자씩 타이핑되듯 표시됨
// 3. Lines 배열의 마지막 줄(예: 12줄이면 Element 11)은 일반 진행에서 제외되고,
//    나머지 줄이 다 끝나면 곧바로 대사 종료 연출(라이트/이미지 페이드, 노래 재생)로 넘어감
// 4. 노래(endingAudioSource)가 musicPlayDuration만큼 재생된 뒤, postSongLineDelay만큼 더 기다렸다가
//    마지막 줄이 해당 화자의 슬롯에 타이핑되며 표시되고, postSongLineHoldDuration만큼 유지된 뒤 페이드 아웃
// 5. 이후 노래를 페이드아웃하며 nextSceneName 씬으로 전환
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
    // [주의] 배열의 마지막 요소(예: 12줄이면 Element 11)는 일반 진행에서 제외되고
    // 노래가 끝난 뒤 별도로 표시되는 "후일담 대사"로 취급됩니다.
    public DialogueLine[] lines = new DialogueLine[]
    {
        new DialogueLine { speaker = Speaker.Player, text = "엄마.. 안주무세요?" },
    };

    [Header("타이밍 설정")]
    public float startDelay = 2f;        // 첫 대사가 뜨기까지 대기 시간 (초기 페이드 시간과 맞추세요)
    public float lineFadeDuration = 0.5f; // 각 줄/슬롯이 나타나고 사라질 때 페이드 시간

    [Header("텍스트 타이핑 효과")]
    // 초당 몇 글자씩 타이핑되듯 표시할지. 0 이하로 두면 한 번에 전체 표시됨
    public float textTypingSpeed = 25f;

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

    [Header("노래 종료 후 후일담 대사 (Lines 배열의 마지막 요소)")]
    // [추가] 노래가 끝난 시점부터 마지막 줄이 뜨기까지 대기할 시간(초)
    public float postSongLineDelay = 2f;
    // 노래가 끝난 뒤 마지막 줄을 몇 초 동안 화면에 유지할지 (타이핑이 다 끝난 뒤부터 카운트됨)
    public float postSongLineHoldDuration = 4f;

    [Header("엔딩 이후 씬 전환 (선택)")]
    // 노래(endingAudioSource) 재생 시간(초). 여기서 조정한 값이 그대로 사용됩니다.
    public float musicPlayDuration = 30f;
    public float musicFadeOutDuration = 2f;
    public string nextSceneName = "Main";

    private int currentLineIndex = -1;
    private bool isTransitioning = false; // 페이드 도중 중복 입력 방지
    private bool isFinished = false;
    private bool isAutoAdvancing = false;
    private DialogueSlot activeSlot; // 현재 화면에 떠있는 슬롯 (화자 전환 시 이걸 먼저 숨김)
    private float songPlayStartTime; // [디버그] endingAudioSource.Play()를 호출한 실제 시각 (Time.time 기준)

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

        // 마지막 줄은 일반 진행에서 제외 (노래가 끝난 뒤 별도로 표시됨)
        int normalLineCount = Mathf.Max(lines.Length - 1, 0);

        if (currentLineIndex >= normalLineCount)
        {
            isFinished = true;
            StartCoroutine(EndDialogueSequence());
            return;
        }

        StartCoroutine(TransitionToLine(lines[currentLineIndex]));
    }

    // 화자에 맞는 슬롯을 찾아서, 화자가 바뀌었다면 슬롯 전체를 페이드 아웃/인하고,
    // 화자가 같으면 슬롯은 계속 켜진 채로 텍스트만 교체함 (깜빡임 없음)
    IEnumerator TransitionToLine(DialogueLine line)
    {
        isTransitioning = true;

        DialogueSlot targetSlot = GetSlotForSpeaker(line.speaker);

        if (targetSlot != null)
        {
            if (activeSlot == targetSlot)
            {
                yield return StartCoroutine(targetSlot.SwapTextRoutine(line.text, textTypingSpeed));
            }
            else
            {
                if (activeSlot != null)
                {
                    yield return StartCoroutine(activeSlot.HideRoutine(lineFadeDuration));
                }

                yield return StartCoroutine(targetSlot.ShowRoutine(line.text, lineFadeDuration, textTypingSpeed));
                activeSlot = targetSlot;
            }
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
            yield return FadeUtility.LerpFloat(spotLight.intensity, 0f, endingLightFadeDuration, v => spotLight.intensity = v);
        }

        if (imageToFadeOut != null)
        {
            yield return FadeUtility.LerpFloat(imageToFadeOut.alpha, 0f, imageFadeOutDuration, v => imageToFadeOut.alpha = v);
        }

        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
        }

        if (endingAudioSource != null)
        {
            endingAudioSource.Play();
            songPlayStartTime = Time.time; // [디버그] 노래가 실제로 재생을 시작한 시각 기록
            float clipLength = endingAudioSource.clip != null ? endingAudioSource.clip.length : -1f;
            Debug.Log($"[LullabyDialogue] 노래 재생 시작 (clip 길이={clipLength:F2}초)");
            StartCoroutine(FadeOutMusicThenLoadScene());
        }
    }

    IEnumerator FadeOutMusicThenLoadScene()
    {
        // [디버그] musicPlayDuration만큼 대기 시작 (인스펙터 값이 제대로 반영되는지 확인용)
        Debug.Log($"[LullabyDialogue] musicPlayDuration 대기 시작: {musicPlayDuration:F2}초");

        // musicPlayDuration만큼 대기 (인스펙터에서 조정한 값이 그대로 적용됨)
        yield return new WaitForSeconds(musicPlayDuration);

        // [디버그] 노래가 실제로 재생을 시작한 시점부터 지금까지 걸린 총 시간
        Debug.Log($"[LullabyDialogue] 노래 시작~끝까지 총 경과 시간: {Time.time - songPlayStartTime:F2}초");

        // [추가] 노래가 끝난 시점부터 postSongLineDelay만큼 더 기다린 뒤 후일담 대사를 띄움
        yield return new WaitForSeconds(postSongLineDelay);

        // Lines 배열의 마지막 줄을 후일담처럼 띄움
        if (lines.Length > 0)
        {
            DialogueLine postSongLine = lines[lines.Length - 1];
            DialogueSlot postSongSlot = GetSlotForSpeaker(postSongLine.speaker);

            if (postSongSlot != null)
            {
                yield return StartCoroutine(postSongSlot.ShowRoutine(postSongLine.text, lineFadeDuration, textTypingSpeed));
                yield return new WaitForSeconds(postSongLineHoldDuration);
                yield return StartCoroutine(postSongSlot.HideRoutine(lineFadeDuration));
            }
        }

        if (endingAudioSource != null)
        {
            yield return FadeUtility.LerpFloat(endingAudioSource.volume, 0f, musicFadeOutDuration, v => endingAudioSource.volume = v);
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}