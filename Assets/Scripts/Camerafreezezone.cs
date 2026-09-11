using UnityEngine;

// 돔방처럼 "카메라가 플레이어를 절대 따라오면 안 되는 구역"의 경계에 배치하는 트리거입니다.
// 플레이어가 들어오면 카메라 추적을 끄고 이 구역용 고정 위치로 스냅시키고,
// 나가면 다시 원래처럼 플레이어를 따라가도록 되돌립니다.
//
// [Unity 설정]
// 1. 돔방 전체를 덮는 빈 오브젝트를 만들고 BoxCollider2D를 붙인 뒤 Is Trigger 체크
// 2. 이 스크립트(CameraFreezeZone)를 그 오브젝트에 붙이기
// 3. Camera Controller 필드에 CameraAndBackgroundController가 붙어있는 오브젝트를 연결
// 4. 돔방 안에서 카메라가 멈춰있을 위치에 빈 오브젝트(예: "DomeCameraPoint")를 만들어서
//    Fixed Camera Point 필드에 연결
// 5. Player 오브젝트의 Tag가 "Player"로 되어있는지 확인 (기본값 Untagged면 트리거가 무시됨)
[RequireComponent(typeof(Collider2D))]
public class CameraFreezeZone : MonoBehaviour
{
    [Header("연동")]
    public CameraAndBackgroundController cameraController;

    [Header("이 구역에서 카메라가 고정될 위치")]
    // 씬에 미리 배치해둔 빈 오브젝트(카메라가 멈출 지점)를 연결하세요
    public Transform fixedCameraPoint;

    [Header("플레이어 인식용 태그")]
    public string playerTag = "Player";

    void Reset()
    {
        // 실수로 물리 충돌체로 쓰지 않도록, 컴포넌트를 처음 붙일 때 Trigger를 기본으로 켜줌
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (cameraController == null || fixedCameraPoint == null) return;

        cameraController.EnterFixedCameraZone(fixedCameraPoint.position);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (cameraController == null) return;

        cameraController.ExitFixedCameraZone();
    }
}