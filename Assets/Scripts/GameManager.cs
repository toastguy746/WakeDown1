using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    [Header("핵심 오브젝트 연동")]
    public PlayerMovement playerScript;
    public Transform ghostTransform;

    [Header("정령 컴패니언 연동")]
    public Spirit spirit;

    [Header("정령 Pet Ghost")]
    public Transform petGhostTransform;

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
    public Color dreamColor =
        new Color(0.6f, 0.2f, 0.9f);

    private bool isSwitching = false;

    private Transform playerTransform;
    private Collider2D playerCollider;

    void Start()
    {
        if (playerScript != null)
        {
            playerTransform = playerScript.transform;
            playerCollider =
                playerScript.GetComponent<Collider2D>();
        }

        if (playerScript != null)
        {
            playerScript.isInDream = isDream;
        }

        UpdateWorldStateButtonColor();

        ResetGhostInitialPosition();
        ResetPetGhostInitialPosition();
    }

    void Update()
    {
        // ============================================================
        // Player Ghost
        // ============================================================

        if (!isSwitching &&
            playerTransform != null &&
            ghostTransform != null)
        {
            ghostTransform.localScale =
                playerTransform.localScale;

            Vector3 playerPos =
                playerTransform.position;

            float ghostY =
                isDream
                ? playerPos.y + worldYInterval
                : playerPos.y - worldYInterval;

            ghostTransform.position =
                new Vector3(
                    playerPos.x,
                    ghostY,
                    playerPos.z
                );
        }

        // ============================================================
        // Pet Ghost
        // ============================================================

        if (!isSwitching &&
            spirit != null &&
            petGhostTransform != null)
        {
            Vector3 spiritPos =
                spirit.transform.position;

            // Pet Ghost는 Spirit의 X를 그대로 따라간다.
            // Y만 두 세계의 간격만큼 차이난다.
            float petGhostY =
                isDream
                ? spiritPos.y + worldYInterval
                : spiritPos.y - worldYInterval;

            petGhostTransform.position =
                new Vector3(
                    spiritPos.x,
                    petGhostY,
                    spiritPos.z
                );
        }

        // ============================================================
        // 세계 전환 입력
        // ============================================================

        if (Input.GetKeyDown(switchWorldKey) &&
            !isSwitching)
        {
            SimulateWorldStateButtonPress();
        }
    }

    // ================================================================
    // 버튼 시뮬레이션
    // ================================================================

    void SimulateWorldStateButtonPress()
    {
        if (worldStateButton == null)
        {
            OnWorldStateButtonClicked();
            return;
        }

        StartCoroutine(
            SimulateButtonPressCoroutine()
        );
    }

    IEnumerator SimulateButtonPressCoroutine()
    {
        PointerEventData pointerData =
            new PointerEventData(EventSystem.current);

        ExecuteEvents.Execute(
            worldStateButton.gameObject,
            pointerData,
            ExecuteEvents.pointerDownHandler
        );

        yield return new WaitForSeconds(0.1f);

        ExecuteEvents.Execute(
            worldStateButton.gameObject,
            pointerData,
            ExecuteEvents.pointerUpHandler
        );

        ExecuteEvents.Execute(
            worldStateButton.gameObject,
            pointerData,
            ExecuteEvents.pointerClickHandler
        );
    }

    // ================================================================
    // 세계 전환 전체 과정
    // ================================================================

    IEnumerator SwitchWorldSequence()
    {
        isSwitching = true;

        if (playerScript != null)
        {
            playerScript.canMove = false;
        }

        // ------------------------------------------------------------
        // 화면 전환
        // ------------------------------------------------------------

        if (transitionUI != null)
        {
            yield return StartCoroutine(
                transitionUI.PlayTransition(
                    isDream,
                    transitionDuration,
                    SwapWorldsAtMidpoint
                )
            );
        }
        else
        {
            SwapWorldsAtMidpoint();
        }

        // ------------------------------------------------------------
        // 여기부터 화면 전환이 완전히 끝난 뒤
        // ------------------------------------------------------------

        // 꿈 → 현실일 경우에만 여기서 Spirit을 복귀시킨다.
        //
        // 따라서 현실로 돌아오는 동안 화면 전환이 진행되는 동안에는
        // Spirit이 아직 잠들었던 위치에 그대로 있고,
        // 화면이 완전히 돌아온 뒤에 Dash가 시작된다.
        if (!isDream && spirit != null)
        {
            spirit.EnterReality();
        }

        if (playerScript != null)
        {
            playerScript.canMove = true;
        }

        isSwitching = false;

        CheckDoorOverlapAfterSwitch();
    }

    // ================================================================
    // 화면이 완전히 어두워졌을 때
    // 실제 세계 위치 교환
    // ================================================================

    void SwapWorldsAtMidpoint()
    {
        // ------------------------------------------------------------
        // Player ↔ Ghost
        // ------------------------------------------------------------

        if (playerTransform != null &&
            ghostTransform != null)
        {
            Vector3 playerCurrentPos =
                playerTransform.position;

            Vector3 ghostCurrentPos =
                ghostTransform.position;

            playerTransform.position =
                ghostCurrentPos;

            ghostTransform.position =
                playerCurrentPos;
        }

        // ------------------------------------------------------------
        // Spirit ↔ Pet Ghost
        // ------------------------------------------------------------

        if (spirit != null &&
            petGhostTransform != null)
        {
            Vector3 spiritCurrentPos =
                spirit.transform.position;

            Vector3 petGhostCurrentPos =
                petGhostTransform.position;

            spirit.transform.position =
                petGhostCurrentPos;

            petGhostTransform.position =
                spiritCurrentPos;
        }

        // ------------------------------------------------------------
        // 세계 상태 변경
        // ------------------------------------------------------------

        isDream = !isDream;

        if (playerScript != null)
        {
            playerScript.isInDream = isDream;
        }

        // ------------------------------------------------------------
        // 카메라
        // ------------------------------------------------------------

        if (cameraController != null)
        {
            float cameraTargetY =
                isDream
                ? -worldYInterval
                : 0f;

            cameraController.SetCameraFixedY(
                cameraTargetY
            );
        }

        // ------------------------------------------------------------
        // 버튼 색상
        // ------------------------------------------------------------

        UpdateWorldStateButtonColor();

        // ------------------------------------------------------------
        // Spirit 상태
        // ------------------------------------------------------------

        if (spirit != null && isDream)
        {
            // --------------------------------------------------------
            // 현실 → 꿈
            //
            // 위치 교환은 위에서 이미 완료됨.
            // 이 시점에 바로 중력을 켠다.
            //
            // 따라서 화면 전환이 끝나기 전부터
            // 꿈에서 Spirit이 떨어진다.
            // --------------------------------------------------------
            spirit.EnterDream();
        }

        // 꿈 → 현실의 경우에는 여기서 EnterReality를 호출하지 않는다.
        // 화면 전환이 완전히 끝난 뒤 SwitchWorldSequence()에서 호출한다.
    }

    // ================================================================
    // 문 겹침 검사
    // ================================================================

    void CheckDoorOverlapAfterSwitch()
    {
        if (playerCollider == null)
            return;

        ExitDoor[] doors =
            FindObjectsByType<ExitDoor>();

        for (int i = 0; i < doors.Length; i++)
        {
            Collider2D doorCollider =
                doors[i].GetComponent<Collider2D>();

            if (doorCollider != null &&
                playerCollider.IsTouching(doorCollider))
            {
                doors[i].TryTriggerExit(
                    playerCollider
                );
            }
        }
    }

    // ================================================================
    // 버튼 색상
    // ================================================================

    void UpdateWorldStateButtonColor()
    {
        if (worldStateButton != null &&
            worldStateButton.image != null)
        {
            worldStateButton.image.color =
                isDream
                ? dreamColor
                : realityColor;
        }
    }

    // ================================================================
    // Player Ghost 초기 위치
    // ================================================================

    void ResetGhostInitialPosition()
    {
        if (ghostTransform != null &&
            playerTransform != null)
        {
            Vector3 playerPos =
                playerTransform.position;

            float targetY =
                isDream
                ? playerPos.y + worldYInterval
                : playerPos.y - worldYInterval;

            ghostTransform.position =
                new Vector3(
                    playerPos.x,
                    targetY,
                    playerPos.z
                );
        }
    }

    // ================================================================
    // Pet Ghost 초기 위치
    // ================================================================

    void ResetPetGhostInitialPosition()
    {
        if (petGhostTransform != null &&
            spirit != null)
        {
            Vector3 spiritPos =
                spirit.transform.position;

            float targetY =
                isDream
                ? spiritPos.y + worldYInterval
                : spiritPos.y - worldYInterval;

            petGhostTransform.position =
                new Vector3(
                    spiritPos.x,
                    targetY,
                    spiritPos.z
                );
        }
    }

    // ================================================================
    // 버튼 OnClick()
    // ================================================================

    public void OnWorldStateButtonClicked()
    {
        if (!isSwitching)
        {
            StartCoroutine(
                SwitchWorldSequence()
            );
        }
    }
}
