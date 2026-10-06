using System.Collections.Generic;
using UnityEngine;

// 보이지 않는 튜토리얼 트리거 오브젝트에 붙이는 스크립트.
// 플레이어가 닿으면 GameManager에게 "이 튜토리얼을 시작해줘"라고 요청만 하고,
// 실제 페이지 넘김/잠금/체인 진행은 전부 GameManager가 코루틴으로 처리합니다.
//
// [멀티 페이지 대사]
// Tutorial Text 인스펙터(TextArea)에서 Enter로 줄을 나누면, 그 줄 하나하나가
// "한 번에 슬롯에 표시되는 페이지"가 됩니다. 플레이어가 advanceKey(GameManager의
// Tutorial Advance Key, 기본 Space)를 누를 때마다 다음 줄로 넘어가고,
// 마지막 줄에서 한 번 더 누르면 이 튜토리얼이 완전히 종료됩니다.
//
// [다른 튜토리얼과 연결]
// Next Tutorial에 다른 TutorialTrigger를 연결하면, 이 튜토리얼이 끝나는 즉시
// 플레이어가 걸어가지 않아도 곧바로 Next Tutorial의 대사가 이어서 재생됩니다.
// (Next Tutorial로 지정된 오브젝트는 자기 콜라이더로 직접 트리거될 필요가 없습니다 —
//  체인으로 소비되는 시점에 자동으로 Destroy됩니다.)
//
// [선택지 분기]
// Choices에 1개 이상 항목을 넣으면, 이 튜토리얼이 끝난 뒤 Next Tutorial 대신 여기서
// 플레이어가 고른 선택지의 target으로 이어집니다. 방향키(위/아래)로 커서를 옮기고
// Tutorial Advance Key(기본 Space)로 확정합니다. 선택 결과는 "다음 대사"만 바꾸는 용도로
// 쓰도록 설계되어 있습니다 — 각 선택지의 target이 서로 다른 후속 트리거를 가리키더라도,
// 그 트리거들의 Next Tutorial을 같은 곳으로 맞춰두면 자연스럽게 스토리가 다시 합류합니다.
//
// [진행 중 잠금]
// 어떤 튜토리얼이 재생 중일 때 다른 튜토리얼 트리거에 플레이어가 닿아도 무시됩니다.
// 이때 해당 트리거는 파괴되지 않으므로, 플레이어가 나갔다가 다시 들어오면 그때 다시 시도됩니다.
//
// [Tutorial Text를 비워두는 경우]
// Tutorial Text를 비워두면 대사도, 대화 슬롯도 전혀 표시되지 않고 조용히 다음 단계(능력 해금,
// 인트로 페이드 해제, 선택지, Next Tutorial)로 넘어갑니다. 텍스트 없이 다른 효과만 내고 싶은
// 트리거(예: 능력만 해금하는 더미 트리거)를 만들 때 사용하세요.
//
// [화면 진동]
// Shake Screen을 체크하면, 이 트리거의 대사가 나타나는 순간 화면이 흔들립니다
// (Tutorial Text가 비어있으면 애초에 대사가 안 나타나므로 진동도 발생하지 않습니다).
//
// [추가] 능력 해금
// 이 튜토리얼의 대사가 전부 끝나는 순간, 체크해둔 능력이 플레이어에게 영구적으로 해금됩니다
// (PlayerMovement.canWalk/canJump, GameManager.canSwitchWorld). 기본 상태에서 플레이어는
// 걷기/점프/C키 월드전환이 전부 잠겨있으므로, 원하는 진행 순서대로 트리거를 배치하고
// 그에 맞는 체크박스를 켜두면 됩니다.
//
// [Unity 설정]
// 1. 보이지 않는(스프라이트 없는) 오브젝트를 만들고 Collider2D(Is Trigger 체크)를 붙이세요.
// 2. 이 스크립트를 붙이고, Game Manager 필드에 GameManager 오브젝트를 연결하세요.
// 3. Target Slot에서 이 튜토리얼이 플레이어용/정령용 슬롯 중 어디에 뜰지 고르세요.
// 4. Tutorial Text에 이 지점에서 보여줄 튜토리얼 대사를 적으세요. 줄을 나누면 페이지가 나뉩니다.
// 5. (선택) 다음 튜토리얼과 이어서 재생하고 싶다면 Next Tutorial에 그 트리거를 연결하세요.
// 6. (선택) 이 튜토리얼이 끝나는 순간 Main 씬의 인트로 페이드를 풀고 싶다면
//    Releases Intro Fade를 체크하세요 (GameManager의 Intro Fade 필드도 연결되어 있어야 합니다).
// 7. (선택) 이 튜토리얼이 끝나는 순간 해금하고 싶은 능력이 있다면 Unlocks Walk / Unlocks Jump /
//    Unlocks World Switch 중 원하는 걸 체크하세요. 여러 개를 동시에 체크해도 됩니다.
// 8. Typing Speed(타이핑 속도), Fade Duration(체인 페이드 인/아웃 시간), Next Tutorial Pause Duration
//    (다음 튜토리얼이 뜨기 전 텀)은 전부 이 트리거마다 따로 설정합니다 (GameManager 전역 설정 아님).
[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    public enum TargetSlot { Player, Spirit }

    [Header("어느 슬롯에 띄울지")]
    public TargetSlot targetSlot = TargetSlot.Player;

    [Header("표시할 튜토리얼 대사 (Enter로 줄바꿈 = 페이지 구분)")]
    [TextArea(2, 4)]
    public string tutorialText;

    [Header("연동")]
    public GameManager gameManager; // 실제 진행 처리는 GameManager가 담당

    [Header("다음 튜토리얼과 연결 (선택)")]
    // 지정하면, 이 튜토리얼이 끝난 직후 플레이어 이동 없이 곧바로 다음 튜토리얼이 이어서 재생됩니다.
    // [주의] 아래 Choices가 1개 이상 있으면 이 필드는 무시되고 Choices 쪽이 우선합니다.
    public TutorialTrigger nextTutorial;

    [System.Serializable]
    public class DialogueChoice
    {
        public string label;           // 선택지로 보여줄 텍스트 (예: "좋았어")
        public TutorialTrigger target; // 이 선택지를 고르면 이어서 재생될 튜토리얼 (비워두면 그대로 종료)
    }

    [Header("선택지 (선택) — 있으면 Next Tutorial 대신 여기서 플레이어가 고른 것으로 이어집니다")]
    // 예: 정령이 질문 → 이 선택지가 플레이어 슬롯에 뜸 → 고른 답변의 target 트리거가 이어서 재생.
    // 두 선택지의 target이 서로 다른 후속 대사를 갖더라도, 그 다음 nextTutorial을 같은 곳으로
    // 맞춰두면 "대답만 다르고 이후 스토리는 합류"하는 구조를 만들 수 있습니다.
    public DialogueChoice[] choices;
    // 선택지 목록을 표시할 슬롯. 보통 "플레이어가 대답을 고르는" 상황이므로 기본값은 Player입니다.
    public TargetSlot choiceSlot = TargetSlot.Player;

    [Header("이 튜토리얼의 연출 설정")]
    // 이 튜토리얼의 텍스트가 타이핑되는 속도(초당 글자 수). 0 이하면 즉시 전체 표시됨
    public float typingSpeed = 25f;
    // 이 튜토리얼이 체인으로 이어받아 나타날 때(페이드 인)와, 체인으로 다음으로 넘어가며
    // 사라질 때(페이드 아웃) 걸리는 시간. 체인이 아닌 단독 시작/종료에는 영향 없음(즉시 표시 유지).
    public float fadeDuration = 0.4f;
    // Next Tutorial이 지정된 경우, 이 튜토리얼이 페이드 아웃으로 완전히 사라진 뒤
    // 다음 튜토리얼이 나타나기 전까지 대기할 텀(초). Next Tutorial이 없으면 사용되지 않음
    public float nextTutorialPauseDuration = 1f;

    [Header("Main 씬 인트로 페이드 해제 (선택)")]
    // 체크하면, 이 튜토리얼이 끝나는 순간 GameManager.introFade의 검은 화면이 풀립니다.
    public bool releasesIntroFade = false;

    [Header("능력 해금 (선택)")]
    // [추가] 체크한 항목이 이 튜토리얼 종료 시점에 플레이어에게 영구적으로 해금됩니다.
    public bool unlocksWalk = false;
    public bool unlocksJump = false;
    public bool unlocksWorldSwitch = false; // C키로 꿈/현실 전환하는 기능

    [Header("화면 진동 (선택)")]
    // [추가] 체크하면 이 튜토리얼의 대사가 시작되는 순간 화면이 크게 흔들립니다
    // (CameraAndBackgroundController.PlayRippleEffect 재사용, GameManager의 Camera Controller 필요).
    public bool shakeScreen = false;
    public float shakeDuration = 0.5f;   // 흔들림 지속 시간(초)
    public float shakeMagnitude = 0.5f;  // 흔들림 강도 (ExitDoor 기본값 0.15보다 훨씬 큰 "큰 흔들림" 기본값)

    [Header("플레이어 인식용 태그")]
    public string playerTag = "Player";

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (gameManager == null) return;

        // 실제 진행 가능 여부(다른 튜토리얼 진행 중인지 등) 판단과 파괴 시점은 GameManager가 책임짐
        gameManager.StartTutorial(this);
    }

    /// <summary>
    /// tutorialText를 줄바꿈(Enter) 기준으로 나눈 "페이지" 배열을 반환합니다.
    /// 인스펙터 TextArea에서 입력한 개행은 문자열에 실제 '\n'(또는 Windows의 '\r\n')으로
    /// 그대로 저장되므로 이 분리가 그대로 적용됩니다. 빈 줄은 페이지로 취급하지 않고 제외합니다.
    /// </summary>
    public string[] GetPages()
    {
        if (string.IsNullOrEmpty(tutorialText)) return new string[0];

        string[] rawLines = tutorialText.Split('\n');
        List<string> pages = new List<string>();

        foreach (string line in rawLines)
        {
            string trimmed = line.TrimEnd('\r'); // Windows 줄바꿈(\r\n) 대비
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                pages.Add(trimmed);
            }
        }

        return pages.ToArray();
    }
}