using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering.Universal; // Light2D 사용을 위해 필요
using UnityEngine.SceneManagement;     // [추가] 씬 전환을 위해 필요

// 엄마가 불러주는 자장가/대사를 한 줄씩 진행시키는 스크립트.
//
// [흐름]
// 1. 씬 시작 후 startDelay(초기 페이드 시간과 맞추세요)만큼 기다렸다가 첫 줄이 페이드 인으로 등장
// 2. Space(또는 오른쪽 위 아이콘 클릭으로 시작하는 자동 모드)로 다음 줄 진행:
//    현재 줄 페이드 아웃 → 텍스트 교체 → 페이드 인
// 3. 마지막 줄에서 한 번 더 진행하면, 그 줄이 페이드 아웃되면서 대사 종료 처리
//    (스팟 라이트를 0으로 서서히 낮추고, 지정한 이미지도 함께 투명화하고, 지정한 오디오를 재생)
// 4. [추가] 오디오 재생 후 musicPlayDuration만큼 지나면 음악을 페이드아웃하며 nextSceneName 씬으로 전환
//
// [Unity 설정]
// 1. 대사가 표시될 TextMeshPro(UGUI) 텍스트 오브젝트를 만들고, 그 오브젝트(또는 부모)에
//    CanvasGroup을 추가한 뒤 Dialogue Text / Text Canvas Group 필드에 각각 연결
// 2. Lines 배열에 대본을 순서대로 채우기
// 3. 화면 오른쪽 위에 아이콘 Image + Button 오브젝트를 만들고(스프라이트는 직접 준비),
//    Auto Advance Icon Button 필드에 연결
// 4. Spot Light 필드에 ep1Manager에서 쓰는 것과 같은 Light2D를 연결
// 5. Ending Audio Source에 종료 시 재생하고 싶은 AudioSource를 연결 (클립은 자유롭게)
// 6. [추가] 자장가 시작과 함께 같이 투명화하고 싶은 이미지가 있다면, 그 이미지에 CanvasGroup을
//    붙이고 Image To Fade Out 필드에 연결
// 7. [추가] Next Scene Name에 전환할 씬 이름(예: "Main")을 정확히 입력 (Build Settings에 등록되어 있어야 함)
public class LullabyDialogue : MonoBehaviour
{
    [Header("연동")]
    public TextMeshProUGUI dialogueText;
    public CanvasGroup textCanvasGroup; // 대사 텍스트의 페이드 인/아웃용

    [Header("대본 (위에서부터 순서대로 한 줄씩)")]
    [TextArea(2, 4)]
    public string[] lines = new string[]
    {
        "엄마.. 안주무세요?",
    };

    [Header("타이밍 설정")]
    public float startDelay = 2f;        // 첫 대사가 뜨기까지 대기 시간 (초기 페이드 시간과 맞추세요)
    public float lineFadeDuration = 0.5f; // 각 줄이 나타나고 사라질 때 페이드 시간

    [Header("입력 설정")]
    public KeyCode advanceKey = KeyCode.Space;
    public Button autoAdvanceIconButton; // 오른쪽 위 아이콘 버튼 (클릭 시 자동 진행 모드 시작)
    public float autoAdvanceDelay = 2f;  // 자동 진행 모드에서 줄이 넘어가는 간격

    [Header("대사 종료 연출")]
    public Light2D spotLight;                 // 마지막 줄이 끝나면 0으로 낮출 스팟 라이트
    public ep1Manager sceneManager;           // [추가] 라이트 왕복 코루틴을 멈추기 위해 필요
    public float endingLightFadeDuration = 2f;
    public AudioSource backgroundMusicSource; // [추가] 자장가가 시작되면 즉시 꺼야 할 기존 배경음악
    public AudioSource endingAudioSource;     // [요청] 원하는 사운드(자장가)를 자유롭게 연결해서 재생

    [Header("자장가 시작 시 함께 투명화할 이미지 (선택)")]
    // [추가] 자장가 종료 연출 때 텍스트/라이트와 함께 서서히 사라질 이미지.
    // 없으면 비워둬도 무방함 (null 체크로 안전 처리됨)
    public CanvasGroup imageToFadeOut;
    public float imageFadeOutDuration = 2f;

    [Header("엔딩 이후 씬 전환 (선택)")]
    // [추가] endingAudioSource 재생 후 musicPlayDuration만큼 지나면 볼륨을 서서히 낮추고
    // nextSceneName 씬으로 전환함. endingAudioSource가 없으면 이 로직은 실행되지 않음
    public float musicPlayDuration = 30f;
    public float musicFadeOutDuration = 2f;
    public string nextSceneName = "Main";

    private int currentLineIndex = -1;
    private bool isTransitioning = false; // 페이드 도중 중복 입력 방지
    private bool isFinished = false;
    private bool isAutoAdvancing = false;

    void Start()
    {
        // [추가] LullabyDialogue와 ep1Manager가 같은 오브젝트에 붙어있다면 인스펙터에서 따로
        // 연결 안 해도 자동으로 찾아서 씀 (수동으로 연결해뒀다면 그 값을 그대로 우선 사용)
        if (sceneManager == null)
        {
            sceneManager = GetComponent<ep1Manager>();
        }

        if (textCanvasGroup != null) textCanvasGroup.alpha = 0f;

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

    // 오른쪽 위 아이콘 클릭 시 호출됨: 자동 진행 모드 시작
    public void OnAutoIconClicked()
    {
        if (isAutoAdvancing || isFinished) return;
        isAutoAdvancing = true;
        StartCoroutine(AutoAdvanceLoop());
    }

    IEnumerator AutoAdvanceLoop()
    {
        AdvanceLine(); // 클릭한 즉시 한 줄 진행
        while (!isFinished)
        {
            yield return new WaitForSeconds(autoAdvanceDelay);
            AdvanceLine();
        }
        isAutoAdvancing = false;
    }

    // 다음 줄로 진행. 이미 마지막 줄이었다면 대사 종료 처리로 넘어감.
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

    IEnumerator TransitionToLine(string line)
    {
        isTransitioning = true;

        // 이전 줄이 떠있다면 먼저 페이드 아웃
        if (textCanvasGroup != null && textCanvasGroup.alpha > 0f)
        {
            yield return FadeCanvasGroup(textCanvasGroup, textCanvasGroup.alpha, 0f, lineFadeDuration);
        }

        if (dialogueText != null) dialogueText.text = line;

        if (textCanvasGroup != null)
        {
            yield return FadeCanvasGroup(textCanvasGroup, 0f, 1f, lineFadeDuration);
        }

        isTransitioning = false;
    }

    // 마지막 줄을 페이드 아웃시키고, 스팟 라이트를 0으로 낮춘 뒤 지정된 사운드(자장가) 재생.
    // [추가] 지정 이미지가 있다면 함께 투명화하고, 오디오 재생 후 일정 시간 뒤 씬 전환까지 처리
    IEnumerator EndDialogueSequence()
    {
        if (textCanvasGroup != null)
        {
            yield return FadeCanvasGroup(textCanvasGroup, textCanvasGroup.alpha, 0f, lineFadeDuration);
        }

        // [추가] ep1Manager가 계속 라이트를 1~1.5로 왕복시키는 코루틴을 돌리고 있으면
        // 아래에서 0으로 낮춰도 매 프레임 다시 덮어써버림 → 먼저 그 코루틴부터 멈춰야 함
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

        // [추가] 스팟 라이트와 별개로, 지정된 이미지가 있다면 함께 투명화
        if (imageToFadeOut != null)
        {
            yield return FadeCanvasGroup(imageToFadeOut, imageToFadeOut.alpha, 0f, imageFadeOutDuration);
        }

        // [추가] 자장가가 시작되는 시점에 기존 배경음악은 즉시 정지
        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
        }

        if (endingAudioSource != null)
        {
            endingAudioSource.Play();
            // [추가] 재생 시작 후 musicPlayDuration이 지나면 페이드아웃하며 씬 전환
            StartCoroutine(FadeOutMusicThenLoadScene());
        }
    }

    // [추가] 자장가 재생 musicPlayDuration초 후, musicFadeOutDuration에 걸쳐 볼륨을 0으로 낮추고
    // nextSceneName 씬을 로드함
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

        if (!string.IsNullOrEmpty("Main"))
        {
            SceneManager.LoadScene("Main");
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