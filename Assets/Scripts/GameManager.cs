using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal; // [추가] Light2D(Global/Spot) 제어를 위해 필요

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
    public KeyCode switchWorldKey = KeyCode.C;

    [Header("꿈/현실 표시 버튼")]
    public Button worldStateButton;
    public Color realityColor = Color.green;
    public Color dreamColor = new Color(0.6f, 0.2f, 0.9f);

    [Header("라이트 연동")]
    // [추가] 현실/꿈 전환에 맞춰 밝기를 바꿀 Light2D들
    public Light2D globalLight;               // 전체를 비추는 Global Light 2D
    public float realityGlobalIntensity = 0.01f;
    public float dreamGlobalIntensity = 0.8f;

    public Light2D spiritSpotLight;            // 정령(Spirit)의 자식으로 붙어있는 Spot Light 2D
    public float realitySpiritSpotIntensity = 2f;
    public float dreamSpiritSpotIntensity = 0f;

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
        if (Input.GetKeyDown(switchWorldKey) && !isSwitching)
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
        // → 화면 전환 중에는 정령이 잠들었던 자리에 그대로 있다가, 화면이 다 밝아진 뒤
        //   눈앞에서 대시/순간이동으로 복귀하는 연출이 나옴 (반대로 꿈에 들어갈 때는
        //   화면이 검은 동안 SwapWorldsAtMidpoint에서 즉시 처리됨 - 아래 참고)
        if (!isDream && spirit != null)
        {
            spirit.EnterReality();
        }

        if (playerScript != null) playerScript.canMove = true;
        isSwitching = false;

        CheckDoorOverlapAfterSwitch();
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

        // 현실 → 꿈으로 들어가는 경우: 위치 교환이 이미 끝났으니 이 시점(화면이 검은 동안)에
        // 곧바로 중력을 켜서 잠들게 함. 꿈 → 현실 복귀는 SwitchWorldSequence()에서 화면이
        // 다 밝아진 뒤 별도로 처리 (EnterReality는 여기서 호출하지 않음)
        if (spirit != null && isDream)
        {
            spirit.EnterDream();
        }
    }

    // ================================================================
    // 문 겹침 검사 (순간이동으로 문과 겹친 채 도착한 경우 대비)
    // ================================================================

    void CheckDoorOverlapAfterSwitch()
    {
        if (playerCollider == null) return;

        ExitDoor[] doors = FindObjectsByType<ExitDoor>(FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            Collider2D doorCollider = doors[i].GetComponent<Collider2D>();
            if (doorCollider != null && playerCollider.IsTouching(doorCollider))
            {
                doors[i].TryTriggerExit(playerCollider);
            }
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

    // [추가] 현재 isDream 값에 맞춰 Global Light와 정령의 Spot Light 밝기를 갱신
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
}