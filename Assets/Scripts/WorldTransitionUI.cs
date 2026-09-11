using System.Collections;
using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필수 추가!

// [분리] 기존 GameManager에 섞여 있던 "화면 연출" 책임(페이드, Sleep/WakeUp 이미지, 팁 텍스트, 전환 효과음)을
// 별도 컴포넌트로 분리했습니다.
// - GameManager: "언제/어떤 상태로 전환할지"만 결정
// - WorldTransitionUI: "전환이 화면에 어떻게 보일지"만 담당
// 이렇게 나누면 연출만 수정할 때 GameManager의 전환 로직을 건드릴 필요가 없어집니다.
public class WorldTransitionUI : MonoBehaviour
{
    [Header("UI 연출 세팅")]
    public CanvasGroup uiCanvasGroup;     // 캔버스의 Canvas Group
    public GameObject sleepImage;         // Sleep 이미지 오브젝트
    public GameObject wakeUpImage;        // WakeUp 이미지 오브젝트
    public TextMeshProUGUI tipText;       // 💥 팁을 띄울 TMP 텍스트 오브젝트

    [Header("오디오 설정 (수동 할당용)")]
    [SerializeField] private AudioSource switchWorldAudioSource;

    // 💥 몽유병 게임 전용 랜덤 팁 리스트
    private string[] tipList = new string[]
    {
        "tip. 렘수면 상태일 때 눈동자가 빠르게 움직이며 이때 꿈을 가장 많이 꿉니다",
        "tip. 잠들기 직전 몸이 움찔하는 것은 뇌가 추락한다고 착각하기 때문입니다",
        "tip. 선천적 시각장애인은 꿈에서도 이미지 없이 소리와 촉감만 느낍니다",
        "tip. 사람은 인생의 약 3분의 1을 잠으로 보낸다고 합니다",
        "tip. 꿈속에서는 시계나 책의 글자가 계속 바뀌어 보입니다",
        "tip. 하품은 전염성이 강해서 글로 봐도 하품이 나올 수 있습니다",
        "tip. 고양이와 개도 잠을 자며 꿈을 꾼다고 알려져 있습니다",
        "tip. 악몽을 자주 꾸는 사람일수록 창의력이 높다는 연구가 있습니다",
        "tip. 잠자는 동안 뇌는 노폐물을 청소하는 작업을 합니다",
        "tip. 꿈은 깬 지 5분 안에 절반을, 10분 뒤엔 대부분 잊혀집니다"
    };

    // [최적화] transitionDuration이 바뀌지 않는 한 WaitForSeconds를 매번 새로 만들지 않고 캐싱해서 재사용
    private WaitForSeconds cachedWaitDarkHold;
    private float cachedTransitionDuration = -1f;

    void Awake()
    {
        // 시작할 때 UI 초기화
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 0f;
        if (sleepImage != null) sleepImage.SetActive(false);
        if (wakeUpImage != null) wakeUpImage.SetActive(false);
        if (tipText != null) tipText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 전환 연출을 재생합니다.
    /// </summary>
    /// <param name="currentIsDream">전환 시작 시점(토글 전)의 isDream 값. Sleep/WakeUp 이미지 선택에 사용.</param>
    /// <param name="transitionDuration">전체 연출 시간(초).</param>
    /// <param name="onScreenFullyDark">화면이 완전히 어두워진 순간 호출되는 콜백. 좌표 교환/카메라 이동 등
    /// GameManager가 처리해야 하는 로직을 이 시점에 실행하도록 전달받습니다.</param>
    public IEnumerator PlayTransition(bool currentIsDream, float transitionDuration, System.Action onScreenFullyDark)
    {
        // [추가] 화면 전환이 시작되는 순간 오디오 소스를 단 한 번 출력합니다.
        if (switchWorldAudioSource != null)
        {
            switchWorldAudioSource.Play();
        }

        // 💥 [핵심 기능] 페이드 인 직전, 10개의 팁 중 하나를 무작위로 뽑아 텍스트창에 적고 켭니다.
        if (tipText != null)
        {
            int randomIndex = Random.Range(0, tipList.Length);
            tipText.text = tipList[randomIndex];
            tipText.gameObject.SetActive(true);
        }

        // 알파가 0일 때 가야 할 세계의 이미지 미리 켜기
        if (sleepImage != null) sleepImage.SetActive(!currentIsDream);
        if (wakeUpImage != null) wakeUpImage.SetActive(currentIsDream);

        // 1. 페이드 인 (화면이 빠르게 어두워짐과 동시에 팁 글씨와 이미지가 한 몸으로 나타남)
        float fadeInTime = transitionDuration * 0.1f;
        float fadeOutTime = transitionDuration * 0.3f;
        float timer = 0f;

        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            if (uiCanvasGroup != null) uiCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInTime);
            yield return null;
        }
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 1f;

        // 2. 완전히 어두워진 순간 → 좌표 교환/카메라 이동 등은 GameManager 쪽 콜백에서 처리
        onScreenFullyDark?.Invoke();

        // 3. 어두운 상태 유지 (유저가 랜덤 팁을 여유롭게 읽는 시간)
        if (cachedWaitDarkHold == null || !Mathf.Approximately(cachedTransitionDuration, transitionDuration))
        {
            cachedWaitDarkHold = new WaitForSeconds(transitionDuration * 0.6f);
            cachedTransitionDuration = transitionDuration;
        }
        yield return cachedWaitDarkHold;

        // 4. 페이드 아웃 (화면이 다시 스르륵 밝아짐)
        timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            if (uiCanvasGroup != null) uiCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutTime);
            yield return null;
        }
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 0f;

        // 5. 완벽히 투명해진 후 모든 UI 요소 정리
        if (sleepImage != null) sleepImage.SetActive(false);
        if (wakeUpImage != null) wakeUpImage.SetActive(false);
        if (tipText != null) tipText.gameObject.SetActive(false);
    }
}