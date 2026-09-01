using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필수 추가!

public class GameManager : MonoBehaviour
{
    [Header("핵심 오브젝트 연동")]
    public PlayerMovement playerScript;   // 플레이어 스크립트 연동
    public Transform ghostTransform;     // Ghost 객체
    public Transform mainCameraTransform; // 메인 카메라

    [Header("세계관 설정")]
    public float worldYInterval = 100000f; // 두 세계 간의 고정 Y축 거리 간격
    public bool isDream = false;         // 현재 꿈 세계인지 여부

    [Header("UI 연출 세팅")]
    public CanvasGroup uiCanvasGroup;     // 캔버스의 Canvas Group
    public GameObject sleepImage;         // Sleep 이미지 오브젝트
    public GameObject wakeUpImage;        // WakeUp 이미지 오브젝트
    public TextMeshProUGUI tipText;       // 💥 팁을 띄울 TMP 텍스트 오브젝트
    public float transitionDuration = 2f; // 전체 연출 시간 (초)

    [Header("오디오 설정 (수동 할당용)")]
    // [추가] 인스펙터에서 직접 AudioSource를 연결할 수 있는 변수입니다.
    [SerializeField] private AudioSource switchWorldAudioSource; 

    private bool isSwitching = false;     // 중복 전환 방지용 변수

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

    void Start()
    {
        // 시작할 때 UI 초기화
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 0f;
        if (sleepImage != null) sleepImage.SetActive(false);
        if (wakeUpImage != null) wakeUpImage.SetActive(false);
        if (tipText != null) tipText.gameObject.SetActive(false); // 팁 글씨 꺼두기

        ResetGhostInitialPosition();
    }

    void Update()
    {
        // 상시로 고스트 위치 동기화
        if (!isSwitching && playerScript != null && ghostTransform != null)
        {
            ghostTransform.localScale = playerScript.transform.localScale;

            if (!isDream)
                ghostTransform.position = new Vector3(playerScript.transform.position.x, playerScript.transform.position.y - worldYInterval, playerScript.transform.position.z);
            else
                ghostTransform.position = new Vector3(playerScript.transform.position.x, playerScript.transform.position.y + worldYInterval, playerScript.transform.position.z);
        }

        // 세계 전환 키(C) 입력 감지
        if (Input.GetKeyDown(KeyCode.C) && !isSwitching)
        {
            StartCoroutine(SwitchWorldSequence());
        }
    }

    IEnumerator SwitchWorldSequence()
    {
        isSwitching = true;
        if (playerScript != null) playerScript.canMove = false;

        // [추가] 화면 전환이 시작되는 순간 오디오 소스를 단 한 번 출력합니다.
        if (switchWorldAudioSource != null)
        {
            switchWorldAudioSource.Play();
        }

        // 💥 [핵심 기능] 페이드 인 직전, 10개의 팁 중 하나를 무작위로 뽑아 텍스트창에 적고 켭니다.
        if (tipText != null)
        {
            int randomIndex = Random.Range(0, tipList.Length); // 0~9 사이의 랜덤 숫자 추출
            tipText.text = tipList[randomIndex];              // 텍스트 컴포넌트에 글씨 주입
            tipText.gameObject.SetActive(true);               // 텍스트 오브젝트 활성화
        }

        // 알파가 0일 때 가야 할 세계의 이미지 미리 켜기
        if (sleepImage != null) sleepImage.SetActive(!isDream);   
        if (wakeUpImage != null) wakeUpImage.SetActive(isDream); 

        // 1. 페이드 인 (화면이 빠르게 어두워짐과 동시에 팁 글씨와 이미지가 한 몸으로 나타남)
        float fadeInTime = transitionDuration * 0.1f; 
        float fadeOutTime = transitionDuration * 0.3f;
        float timer = 0f;
        
        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInTime);
            yield return null;
        }
        uiCanvasGroup.alpha = 1f;

        // 2. 완전히 어두워진 순간 좌표 교환
        Vector3 myCurrentPos = playerScript.transform.position;
        Vector3 ghostCurrentPos = ghostTransform.position;
        
        playerScript.transform.position = ghostCurrentPos;
        ghostTransform.position = myCurrentPos;

        isDream = !isDream;

        if (mainCameraTransform != null)
        {
            float cameraTargetY = isDream ? -worldYInterval : 0f;
            mainCameraTransform.position = new Vector3(0f, cameraTargetY, -10f);
        }

        // 3. 어두운 상태 유지 (유저가 랜덤 팁을 여유롭게 읽는 시간)
        yield return new WaitForSeconds(transitionDuration * 0.6f);

        // 4. 페이드 아웃 (화면이 다시 스르륵 밝아짐)
        timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutTime);
            yield return null;
        }
        uiCanvasGroup.alpha = 0f;

        // 5. 완벽히 투명해진 후 모든 UI 요소 정리
        if (sleepImage != null) sleepImage.SetActive(false);
        if (wakeUpImage != null) wakeUpImage.SetActive(false);
        if (tipText != null) tipText.gameObject.SetActive(false); // 💥 팁 글씨 다시 끄기
        
        if (playerScript != null) playerScript.canMove = true;
        isSwitching = false;
    }

    void ResetGhostInitialPosition()
    {
        if (ghostTransform != null && playerScript != null)
        {
            float targetY = isDream ? playerScript.transform.position.y + worldYInterval : playerScript.transform.position.y - worldYInterval;
            ghostTransform.position = new Vector3(playerScript.transform.position.x, targetY, playerScript.transform.position.z);
        }
    }
}