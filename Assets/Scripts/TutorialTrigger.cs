using UnityEngine;

// 보이지 않는 튜토리얼 트리거 오브젝트에 붙이는 스크립트.
// 플레이어가 닿으면 지정한 슬롯(플레이어용/정령용)에 튜토리얼 대사를 띄우고,
// 한 번 실행된 뒤 스스로 사라집니다.
//
// [Unity 설정]
// 1. 보이지 않는(스프라이트 없는) 오브젝트를 만들고 Collider2D(Is Trigger 체크)를 붙이세요.
// 2. 이 스크립트를 붙이고, Game Manager 필드에 GameManager 오브젝트를 연결하세요.
// 3. Target Slot에서 이 튜토리얼이 플레이어용/정령용 슬롯 중 어디에 뜰지 고르세요.
// 4. Tutorial Text에 이 지점에서 보여줄 튜토리얼 대사를 적으세요.
[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    public enum TargetSlot { Player, Spirit }

    [Header("어느 슬롯에 띄울지")]
    public TargetSlot targetSlot = TargetSlot.Player;

    [Header("표시할 튜토리얼 대사")]
    [TextArea(2, 4)]
    public string tutorialText;

    [Header("연동")]
    public GameManager gameManager; // 실제 슬롯 표시 처리는 GameManager가 담당

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

        gameManager.ShowTutorialDialogue(targetSlot, tutorialText);

        Destroy(gameObject); // [요청] 한 번 진행된 튜토리얼 객체는 즉시 삭제
    }
}