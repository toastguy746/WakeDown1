using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal; // Light2D(Global/Spot) 제어를 위해 필요

public class GameManager : MonoBehaviour
{
    [Header("핵심 오브젝트 연동")]
    public PlayerMovement playerScript;
    public Transform ghostTransform;

    [Header("정령 컴패니언 연동")]
    public Spirit spirit;
    public Transform petGhostTransform; // 정령의 꿈 세계 고스트

    [Header("카메라/배경 연동")]
    public CameraAndBackgroundController cameraController;

    [Header("세계관 설정")]
    public float worldYInterval = 12f;
    public bool isDream = false;

    [Header("화면 전환 연출 연동")]
    public WorldTransitionUI transitionUI;
    public float transitionDuration = 2f;

    [Header("월드 전환 입력 설정")]
    // [추가] 기본적으로 잠겨있다가, Unlocks World Switch가 체크된 TutorialTrigger가 끝나면 true로 바뀜.
    // false인 동안은 C키를 눌러도 아무 일도 일어나지 않습니다.
    public bool canSwitchWorld = false;
    public KeyCode switchWorldKey = KeyCode.C;

    [Header("꿈/현실 표시 버튼")]
    public Button worldStateButton;
    public Color realityColor = Color.green;
    public Color dreamColor = new Color(0.6f, 0.2f, 0.9f);

    [Header("라이트 연동")]
    // 현실/꿈 전환에 맞춰 밝기를 바꿀 Light2D들
    public Light2D globalLight;               // 전체를 비추는 Global Light 2D
    public float realityGlobalIntensity = 0.01f;
    public float dreamGlobalIntensity = 0.8f;

    public Light2D spiritSpotLight;            // 정령(Spirit)의 자식으로 붙어있는 Spot Light 2D
    public float realitySpiritSpotIntensity = 2f;
    public float dreamSpiritSpotIntensity = 0f;

    [Header("튜토리얼 대화 슬롯")]
    // 플레이어/정령 각각의 대화 슬롯. TutorialTrigger가 닿았을 때 이 중 하나에 대사를 띄움
    public DialogueSlot playerDialogueSlot;
    public DialogueSlot spiritDialogueSlot;

    [Header("튜토리얼 진행 설정")]
    // 튜토리얼 페이지를 넘길 때 사용할 키. 모든 튜토리얼이 이 키 하나를 공통으로 사용합니다.
    public KeyCode tutorialAdvanceKey = KeyCode.Space;
    // Main 씬 시작 시 깔아둔 인트로 페이드. releasesIntroFade가 체크된 튜토리얼이 끝나면 이걸 걷어냄
    public MainSceneIntroFade introFade;

    // 타이핑 속도/체인 페이드 인·아웃 시간/다음 튜토리얼 대기 텀은 GameManager가 아니라
    // 각 TutorialTrigger가 자기 자신의 값(typingSpeed, fadeDuration, nextTutorialPauseDuration)으로 갖고 있습니다.

    private bool isTutorialInProgress = false; // 다른 튜토리얼이 재생 중인지 잠금 플래그
    private bool tutorialDisplayedContent = false; // [추가] 방금 트리거가 실제로 텍스트를 보여줬는지 (비어있으면 false)
    private TutorialTrigger pendingChoiceResult; // [추가] RunChoiceSelection이 고른 결과를 담아두는 임시 필드
    private DialogueSlot pendingChoiceSlot; // [추가] 선택지 목록을 표시했던 슬롯 (선택 후 치워주기 위해 기억해둠)

    private bool isSwitching = false;
    private Transform playerTransform;
    private Collider2D playerCollider;

    void Start()
    {
        if (playerScript != null)
        {
            playerTransform = playerScript.transform;
            playerCollider = playerScript.GetComponent<Collider2D>();
            playerScript.isInDream = isDream;
        }

        UpdateWorldStateButtonColor();
        UpdateLightsForCurrentWorld();
        ResetGhostInitialPosition();
        ResetPetGhostInitialPosition();
    }

    void Update()
    {
        // 플레이어 고스트: 꿈/현실 반대편에서 항상 플레이어와 같은 X, 세계 간격만큼 다른 Y를 유지
        if (!isSwitching && playerTransform != null && ghostTransform != null)
        {
            ghostTransform.localScale = playerTransform.localScale;
            Vector3 playerPos = playerTransform.position;
            float ghostY = isDream ? playerPos.y + worldYInterval : playerPos.y - worldYInterval;
            ghostTransform.position = new Vector3(playerPos.x, ghostY, playerPos.z);
        }

        // 정령 고스트: 정령의 X를 그대로 따라가되, Y만 세계 간격만큼 차이
        if (!isSwitching && spirit != null && petGhostTransform != null)
        {
            Vector3 spiritPos = spirit.transform.position;
            float petGhostY = isDream ? spiritPos.y + worldYInterval : spiritPos.y - worldYInterval;
            petGhostTransform.position = new Vector3(spiritPos.x, petGhostY, spiritPos.z);
        }

        // 세계 전환 키 입력 → 버튼 클릭 시뮬레이션으로 통일
        // ([변경] canSwitchWorld가 해금되지 않았거나, 대화창(튜토리얼)이 떠있는 동안에는 무시)
        if (canSwitchWorld && Input.GetKeyDown(switchWorldKey) && !isSwitching && !isTutorialInProgress)
        {
            SimulateWorldStateButtonPress();
        }
    }

    // ================================================================
    // 버튼 클릭 시뮬레이션 (C키 입력을 실제 버튼 클릭처럼 재현)
    // ================================================================

    void SimulateWorldStateButtonPress()
    {
        if (worldStateButton == null)
        {
            OnWorldStateButtonClicked();
            return;
        }
        StartCoroutine(SimulateButtonPressCoroutine());
    }

    IEnumerator SimulateButtonPressCoroutine()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);

        ExecuteEvents.Execute(worldStateButton.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSeconds(0.1f);
        ExecuteEvents.Execute(worldStateButton.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(worldStateButton.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
    }

    // ================================================================
    // 세계 전환 전체 시퀀스
    // ================================================================

    IEnumerator SwitchWorldSequence()
    {
        isSwitching = true;
        if (playerScript != null) playerScript.canMove = false;

        if (transitionUI != null)
        {
            yield return StartCoroutine(transitionUI.PlayTransition(isDream, transitionDuration, SwapWorldsAtMidpoint));
        }
        else
        {
            SwapWorldsAtMidpoint();
        }

        // 꿈 → 현실로 돌아온 경우에만 여기(화면이 완전히 밝아진 뒤)에서 정령을 복귀시킴.
        if (!isDream && spirit != null)
        {
            spirit.EnterReality();
        }

        if (playerScript != null) playerScript.canMove = true;
        isSwitching = false;
    }

    // 화면이 완전히 어두워진 순간 실행: 좌표 교환 + 세계 상태 토글 + 카메라/버튼 갱신
    void SwapWorldsAtMidpoint()
    {
        if (playerTransform != null && ghostTransform != null)
        {
            Vector3 playerCurrentPos = playerTransform.position;
            Vector3 ghostCurrentPos = ghostTransform.position;
            playerTransform.position = ghostCurrentPos;
            ghostTransform.position = playerCurrentPos;
        }

        if (spirit != null && petGhostTransform != null)
        {
            Vector3 spiritCurrentPos = spirit.transform.position;
            Vector3 petGhostCurrentPos = petGhostTransform.position;
            spirit.transform.position = petGhostCurrentPos;
            petGhostTransform.position = spiritCurrentPos;
        }

        isDream = !isDream;

        if (playerScript != null) playerScript.isInDream = isDream;

        if (cameraController != null)
        {
            float cameraTargetY = isDream ? -worldYInterval : 0f;
            cameraController.SetCameraFixedY(cameraTargetY);
        }

        UpdateWorldStateButtonColor();
        UpdateLightsForCurrentWorld();

        if (spirit != null && isDream)
        {
            spirit.EnterDream();
        }
    }

    // ================================================================
    // 버튼 색상 / 초기 위치 / 외부 진입점
    // ================================================================

    void UpdateWorldStateButtonColor()
    {
        if (worldStateButton != null && worldStateButton.image != null)
        {
            worldStateButton.image.color = isDream ? dreamColor : realityColor;
        }
    }

    void UpdateLightsForCurrentWorld()
    {
        if (globalLight != null)
        {
            globalLight.intensity = isDream ? dreamGlobalIntensity : realityGlobalIntensity;
        }

        if (spiritSpotLight != null)
        {
            spiritSpotLight.intensity = isDream ? dreamSpiritSpotIntensity : realitySpiritSpotIntensity;
        }
    }

    void ResetGhostInitialPosition()
    {
        if (ghostTransform == null || playerTransform == null) return;
        Vector3 playerPos = playerTransform.position;
        float targetY = isDream ? playerPos.y + worldYInterval : playerPos.y - worldYInterval;
        ghostTransform.position = new Vector3(playerPos.x, targetY, playerPos.z);
    }

    void ResetPetGhostInitialPosition()
    {
        if (petGhostTransform == null || spirit == null) return;
        Vector3 spiritPos = spirit.transform.position;
        float targetY = isDream ? spiritPos.y + worldYInterval : spiritPos.y - worldYInterval;
        petGhostTransform.position = new Vector3(spiritPos.x, targetY, spiritPos.z);
    }

    public void OnWorldStateButtonClicked()
    {
        if (!isSwitching) StartCoroutine(SwitchWorldSequence());
    }

    // ================================================================
    // 튜토리얼 대화 슬롯 (TutorialTrigger가 호출)
    // ================================================================

    /// <summary>
    /// TutorialTrigger가 플레이어와 충돌했을 때 호출합니다. 이미 다른 튜토리얼이 진행 중이거나
    /// C키로 세계 전환(꿈/현실) 중일 때는 아무 일도 하지 않고 무시합니다.
    /// </summary>
    public void StartTutorial(TutorialTrigger trigger)
    {
        if (isTutorialInProgress || isSwitching || trigger == null) return;
        StartCoroutine(RunTutorialChain(trigger));
    }

    IEnumerator RunTutorialChain(TutorialTrigger firstTrigger)
    {
        isTutorialInProgress = true;
        if (playerScript != null) playerScript.canMove = false;

        Debug.Log($"[Tutorial] 체인 시작: '{firstTrigger.name}'");

        TutorialTrigger current = firstTrigger;
        bool fadeInThisOne = false;

        while (current != null)
        {
            DialogueSlot slot = GetSlotForTutorial(current);

            yield return StartCoroutine(RunSingleTutorial(current, slot, fadeInThisOne));

            // [추가] 이 트리거가 끝나는 시점에 체크된 능력을 해금
            ApplyUnlocksFor(current);

            if (current.releasesIntroFade)
            {
                if (introFade != null)
                {
                    Debug.Log("[Tutorial] Releases Intro Fade 체크됨 → introFade.ReleaseFade() 호출");
                    introFade.ReleaseFade();
                }
                else
                {
                    Debug.LogWarning($"[Tutorial] '{current.name}'는 Releases Intro Fade가 체크되어 있지만, GameManager의 Intro Fade 필드가 비어있어 페이드를 해제할 수 없습니다.");
                }
            }

            // [추가] Choices가 있으면 Next Tutorial 대신 플레이어가 고른 선택지의 target으로 이어짐
            TutorialTrigger next;

            if (current.choices != null && current.choices.Length > 0)
            {
                // [수정] 선택지 슬롯(보통 Player)을 띄우기 전에, 현재 질문이 떠있던 슬롯(보통 Spirit)부터
                // 먼저 정리해야 두 텍스트가 겹쳐 보이지 않음. 단, 애초에 대사를 표시한 적이 없다면
                // (tutorialDisplayedContent == false) 숨길 것도 없으므로 이 단계를 건너뜀
                if (slot != null && tutorialDisplayedContent)
                {
                    Debug.Log($"[Tutorial] '{current.name}' 선택지 진입 전 기존 슬롯 정리 ({current.fadeDuration}초 페이드아웃 + {current.nextTutorialPauseDuration}초 텀)");
                    yield return StartCoroutine(slot.HideRoutine(current.fadeDuration));
                    yield return new WaitForSeconds(current.nextTutorialPauseDuration);
                }

                yield return StartCoroutine(RunChoiceSelection(current));
                next = pendingChoiceResult;

                // [추가] 선택지 목록을 치워줌. next가 있으면 그쪽의 ShowRoutine이 알아서 덮어쓰긴 하지만,
                // next가 없는 경우(선택지에서 바로 종료) 여기서 지워주지 않으면 화면에 영원히 남아있었음
                if (pendingChoiceSlot != null)
                {
                    if (next != null)
                    {
                        yield return StartCoroutine(pendingChoiceSlot.HideRoutine(current.fadeDuration));
                        yield return new WaitForSeconds(current.nextTutorialPauseDuration);
                    }
                    else
                    {
                        pendingChoiceSlot.Hide();
                    }
                }
            }
            else
            {
                next = current.nextTutorial;

                // [변경] 대사를 표시한 적이 없다면(tutorialDisplayedContent == false) 페이드/텀/Hide 전부
                // 불필요하므로 전체를 건너뜀 — 텍스트 없는 트리거는 정말로 아무 연출도 없이 조용히 지나감
                if (slot != null && tutorialDisplayedContent)
                {
                    if (next != null)
                    {
                        Debug.Log($"[Tutorial] 체인 전환: '{current.name}' → '{next.name}' ({current.fadeDuration}초 페이드아웃 + {current.nextTutorialPauseDuration}초 텀)");
                        yield return StartCoroutine(slot.HideRoutine(current.fadeDuration));
                        yield return new WaitForSeconds(current.nextTutorialPauseDuration);
                    }
                    else
                    {
                        slot.Hide();
                    }
                }
            }

            if (current.gameObject != null) Destroy(current.gameObject);
            current = next;
            fadeInThisOne = current != null;

            if (current != null) Debug.Log($"[Tutorial] 체인 연결됨 → 다음: '{current.name}'");
        }

        if (playerScript != null) playerScript.canMove = true;
        isTutorialInProgress = false;
        Debug.Log("[Tutorial] 체인 전체 종료, 잠금 해제됨 (canMove 복구)");
    }

    // [추가] trigger에 체크된 능력 해금 플래그를 실제 플레이어/GameManager 상태에 반영
    private void ApplyUnlocksFor(TutorialTrigger trigger)
    {
        if (trigger.unlocksWalk && playerScript != null)
        {
            playerScript.canWalk = true;
            Debug.Log($"[Tutorial] '{trigger.name}' 완료 → 걷기(canWalk) 해금됨");
        }

        if (trigger.unlocksJump && playerScript != null)
        {
            playerScript.canJump = true;
            Debug.Log($"[Tutorial] '{trigger.name}' 완료 → 점프(canJump) 해금됨");
        }

        if (trigger.unlocksWorldSwitch)
        {
            canSwitchWorld = true;
            Debug.Log($"[Tutorial] '{trigger.name}' 완료 → 월드전환(canSwitchWorld) 해금됨");
        }
    }

    IEnumerator RunSingleTutorial(TutorialTrigger trigger, DialogueSlot slot, bool fadeIn)
    {
        tutorialDisplayedContent = false; // [추가] 이번 트리거가 실제로 뭔가 보여줬는지 호출부(RunTutorialChain)에 알려주는 플래그
        string[] pages = trigger.GetPages();

        if (slot == null)
        {
            Debug.LogWarning($"[Tutorial] '{trigger.name}'의 대상 슬롯이 GameManager에 연결되어 있지 않습니다 (Player/Spirit Dialogue Slot 확인 필요). 대사 표시 없이 건너뜁니다.");
            yield break;
        }
        // [변경] 대사가 비어있는 건 에러가 아니라 "일부러 텍스트 없이 쓰는" 정상적인 경우일 수 있음
        // (예: 능력 해금/인트로 페이드 해제/선택지 분기용으로만 쓰이는 트리거). 그래서 경고가 아니라
        // 평범한 로그로 남기고, 슬롯은 아예 건드리지 않은 채 조용히 넘어감
        if (pages.Length == 0)
        {
            Debug.Log($"[Tutorial] '{trigger.name}'은(는) Tutorial Text가 비어있어 대사 표시 없이 다음으로 넘어갑니다.");

            // [추가] 대사가 없어도 진동만 내는 더미 트리거는 지원해야 하므로, 여기서도 진동 체크는 함.
            // 이 경우 흔들림을 보여줄 다른 연출(텍스트)이 없으므로, 진동이 다 끝날 때까지 기다렸다가
            // 다음 단계(능력 해금/체인 등)로 넘어감 — 안 그러면 흔들리기 시작하자마자 바로 다음으로 넘어가버림
            if (trigger.shakeScreen)
            {
                if (cameraController != null)
                {
                    Debug.Log($"[Tutorial] '{trigger.name}' 진동 연출 시작 (지속={trigger.shakeDuration}초, 강도={trigger.shakeMagnitude}) — 대사가 없어 진동이 끝날 때까지 대기합니다");
                    cameraController.PlayRippleEffect(trigger.shakeDuration, trigger.shakeMagnitude);
                    yield return new WaitForSeconds(trigger.shakeDuration);
                }
                else
                {
                    Debug.LogWarning($"[Tutorial] '{trigger.name}'는 Shake Screen이 체크되어 있지만, GameManager의 Camera Controller 필드가 비어있어 진동을 재생할 수 없습니다.");
                }
            }

            yield break;
        }

        tutorialDisplayedContent = true;

        // [추가] 진동 체크 — 대사가 시작되는 시점에 맞춰 화면을 흔듦
        if (trigger.shakeScreen)
        {
            if (cameraController != null)
            {
                Debug.Log($"[Tutorial] '{trigger.name}' 진동 연출 시작 (지속={trigger.shakeDuration}초, 강도={trigger.shakeMagnitude})");
                cameraController.PlayRippleEffect(trigger.shakeDuration, trigger.shakeMagnitude);
            }
            else
            {
                Debug.LogWarning($"[Tutorial] '{trigger.name}'는 Shake Screen이 체크되어 있지만, GameManager의 Camera Controller 필드가 비어있어 진동을 재생할 수 없습니다.");
            }
        }

        Debug.Log($"[Tutorial] '{trigger.name}' 시작 — 페이지 {pages.Length}개, 슬롯={slot.name}, 페이드인={fadeIn}");

        for (int i = 0; i < pages.Length; i++)
        {
            IEnumerator displayRoutine;
            if (i == 0)
            {
                if (fadeIn)
                {
                    displayRoutine = slot.ShowRoutine(pages[i], trigger.fadeDuration, trigger.typingSpeed);
                }
                else
                {
                    displayRoutine = slot.ShowRoutine(pages[i], 0f, trigger.typingSpeed);
                }
            }
            else
            {
                displayRoutine = slot.SwapTextRoutine(pages[i], trigger.typingSpeed);
            }

            yield return StartCoroutine(RunPageDisplayWithSkip(slot, displayRoutine));

            Debug.Log($"[Tutorial] 페이지 {i + 1}/{pages.Length} 표시 완료: \"{pages[i]}\" — {tutorialAdvanceKey} 입력 대기 중");
            yield return WaitForTutorialAdvanceKey();
            Debug.Log($"[Tutorial] {tutorialAdvanceKey} 입력 감지됨");
        }

        Debug.Log($"[Tutorial] '{trigger.name}' 대사 표시 종료");
    }

    private DialogueSlot GetSlotForTutorial(TutorialTrigger trigger)
    {
        return trigger.targetSlot == TutorialTrigger.TargetSlot.Player ? playerDialogueSlot : spiritDialogueSlot;
    }

    // [추가] trigger.choices를 trigger.choiceSlot에 목록으로 띄우고, 방향키로 커서를 옮기다가
    // tutorialAdvanceKey로 확정하면 pendingChoiceResult에 선택된 target을 담아둡니다.
    // 이 선택지 목록을 띄웠던 슬롯은 pendingChoiceSlot에 기억해두고, 호출부(RunTutorialChain)가
    // 선택 확정 직후 책임지고 정리합니다 (다음 트리거가 없을 수도 있어서, 다음 트리거의
    // ShowRoutine에만 기대면 그 경우엔 아무도 안 지워주는 문제가 있었습니다).
    IEnumerator RunChoiceSelection(TutorialTrigger trigger)
    {
        DialogueSlot choiceSlotRef = trigger.choiceSlot == TutorialTrigger.TargetSlot.Player ? playerDialogueSlot : spiritDialogueSlot;
        pendingChoiceSlot = choiceSlotRef;

        if (choiceSlotRef == null)
        {
            Debug.LogWarning($"[Tutorial] '{trigger.name}'의 Choice Slot이 GameManager에 연결되어 있지 않습니다. 선택지 없이 종료합니다.");
            pendingChoiceResult = null;
            yield break;
        }

        int selectedIndex = 0;
        RenderChoices(choiceSlotRef, trigger.choices, selectedIndex);
        Debug.Log($"[Tutorial] '{trigger.name}' 선택지 {trigger.choices.Length}개 표시 — 방향키로 고르고 {tutorialAdvanceKey}로 확정");

        yield return null; // 직전 입력이 그대로 씹혀서 즉시 확정되는 것을 방지

        while (true)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                selectedIndex = (selectedIndex + 1) % trigger.choices.Length;
                RenderChoices(choiceSlotRef, trigger.choices, selectedIndex);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                selectedIndex = (selectedIndex - 1 + trigger.choices.Length) % trigger.choices.Length;
                RenderChoices(choiceSlotRef, trigger.choices, selectedIndex);
            }
            else if (Input.GetKeyDown(tutorialAdvanceKey))
            {
                break;
            }

            yield return null;
        }

        TutorialTrigger.DialogueChoice chosen = trigger.choices[selectedIndex];
        pendingChoiceResult = chosen.target;
        Debug.Log($"[Tutorial] 선택지 확정: \"{chosen.label}\" → 다음: {(chosen.target != null ? chosen.target.name : "(없음, 종료)")}");

        // [추가] 고르지 않은 나머지 선택지들의 target은 다시 쓰일 일이 없으므로 여기서 정리함
        // (같은 target을 여러 선택지가 공유하는 합류 구조일 수 있으니, 선택된 target과 같은 건 건너뜀)
        foreach (TutorialTrigger.DialogueChoice choice in trigger.choices)
        {
            if (choice == chosen) continue;
            if (choice.target == null || choice.target == chosen.target) continue;
            if (choice.target.gameObject == null) continue;

            Debug.Log($"[Tutorial] 선택되지 않은 분기 정리: '{choice.target.name}' Destroy");
            Destroy(choice.target.gameObject);
        }
    }

    // 선택지 목록을 한 줄씩, 현재 커서가 있는 항목만 "> " 표시로 구분해서 슬롯에 즉시 표시
    // [수정] 유니코드 화살표(▶)는 한글 SDF 폰트 아틀라스에 없는 경우가 많아 네모(□)로 깨지므로,
    // 어떤 폰트에도 항상 존재하는 일반 ASCII ">" 문자로 대체함
    private void RenderChoices(DialogueSlot slot, TutorialTrigger.DialogueChoice[] choices, int selectedIndex)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < choices.Length; i++)
        {
            sb.Append(i == selectedIndex ? "> " : "   ");
            sb.Append(choices[i].label);
            sb.Append('\n'); // 안내 문구를 위해 항상 개행 (마지막 선택지 뒤에도 붙음)
        }

        // [추가] 조작법을 몰라서 헤매지 않도록 화면에 직접 안내 문구를 같이 표시
        sb.Append($"\n(↑/↓ 이동, {tutorialAdvanceKey}로 선택)");

        slot.Show(sb.ToString());
    }

    IEnumerator RunPageDisplayWithSkip(DialogueSlot slot, IEnumerator displayRoutine)
    {
        bool isDone = false;
        StartCoroutine(RunAndMarkDone(displayRoutine, () => isDone = true));

        yield return null;
        while (!isDone)
        {
            if (Input.GetKeyDown(tutorialAdvanceKey))
            {
                slot.SkipTyping();
            }
            yield return null;
        }
    }

    IEnumerator RunAndMarkDone(IEnumerator routine, System.Action onDone)
    {
        yield return StartCoroutine(routine);
        onDone();
    }

    IEnumerator WaitForTutorialAdvanceKey()
    {
        yield return null;
        while (!Input.GetKeyDown(tutorialAdvanceKey))
        {
            yield return null;
        }
    }
}