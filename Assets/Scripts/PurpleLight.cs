using System.Collections.Generic;
using UnityEngine;

// Ep.3 Azocci 전용: 보라빛 에너지 오브젝트 "자신"에 직접 붙이는 스크립트.
// - 플레이어가 닿으면 "현실 → 꿈" 이동권 1회를 얻고, 에너지는 Destroy/SetActive(false)가 아니라
//   렌더러/콜라이더/자식 오브젝트만 꺼서 숨깁니다 (스크립트가 계속 살아있어야 하기 때문).
// - 꿈으로 이동하면 GameManager가 RespawnEnergy()를 호출해서 같은 자리에 다시 보이게 합니다.
// - 꿈 → 현실은 이동권과 상관없이 언제든 가능합니다 (GameManager에서 처리).
// - GameManager가 씬 시작 시 이 스크립트를 자동으로 찾으므로 인스펙터 연결이 필요 없습니다.
//   이 스크립트가 없는 씬에서는 C키가 기존처럼 자유롭게 동작합니다.
//
// [수정] 구슬이 여러 개여도 이동권은 "최대 1개"입니다 (모든 구슬이 하나의 이동권 상태를 공유).
// - 이동권이 없을 때 구슬을 먹으면 이동권 1개 획득 + 그 구슬은 사라짐.
// - [변경] 이미 이동권을 갖고 있을 때도 다른 구슬을 먹을 수 있음 (구슬은 사라지고 사운드도 재생됨).
//   단, 이동권은 늘어나지 않고 그대로 1개로 유지됨 (이동권 중복 보유 불가).
// - 꿈으로 이동해 이동권을 쓰면, 씬의 모든 구슬이 같은 자리에 다시 나타남.
// - GameManager는 씬의 구슬 중 하나만 참조하지만, 상태가 공유되므로 어느 구슬을 참조해도 결과가 같습니다.
//   (단, 대사 문구는 GameManager가 찾은 구슬의 것을 쓰므로 구슬마다 같은 문구로 맞춰주세요)
//
// [Unity 설정] 에너지 오브젝트에 Collider2D(Is Trigger 체크) + 이 스크립트를 붙이세요.
public class PurpleLight : MonoBehaviour
{
    [Header("C키 대사")]
    [TextArea] public string blockedMessage = "지금은 이동할 수 없는 것 같다.";
    [TextArea] public string askToDreamMessage = "거울 세계로 이동할까?";
    [TextArea] public string askToRealityMessage = "현실 세계로 돌아갈까?";
    public string yesLabel = "예";
    public string noLabel = "아니오";

    [Header("플레이어 인식")]
    public string playerTag = "Player";

    [Header("획득 사운드")]
    // 플레이어가 에너지를 먹는 순간 한 번 재생할 오디오 소스.
    // 비워두면 이 오브젝트(또는 자식)에 붙은 AudioSource를 자동으로 찾아 씁니다.
    // 해당 AudioSource의 Play On Awake와 Loop는 꺼두세요.
    public AudioSource pickupAudioSource;

    [Header("테스트용")]
    // 체크하면 먹어도 에너지가 사라지지 않고, 닿을 때마다 획득 사운드가 계속 재생됩니다.
    // (사운드 확인용. 확인이 끝나면 반드시 꺼두세요)
    public bool keepVisibleOnPickup = false;

    // ================================================================
    // 모든 구슬이 공유하는 상태
    // ================================================================

    // 씬에 존재하는 모든 구슬 (이동권 사용 시 전부 되살리기 위함)
    private static readonly List<PurpleLight> allOrbs = new List<PurpleLight>();

    // 현실 → 꿈 이동권 보유 여부 (최대 1개이므로 bool)
    private static bool hasChargeShared = false;

    // 도메인 리로드가 꺼진 에디터 설정에서도 이전 플레이의 상태가 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        allOrbs.Clear();
        hasChargeShared = false;
    }

    /// <summary>현실 → 꿈 이동권(최대 1개)을 갖고 있는지</summary>
    public bool HasCharge => hasChargeShared;

    // ================================================================

    private Renderer[] ownRenderers;
    private Collider2D ownCollider;

    void Awake()
    {
        ownRenderers = GetComponents<Renderer>();
        ownCollider = GetComponent<Collider2D>();

        if (pickupAudioSource == null)
            pickupAudioSource = GetComponentInChildren<AudioSource>(true);

        if (ownCollider == null)
            Debug.LogWarning("[PurpleLight] 이 오브젝트에 Collider2D(Is Trigger)가 없어서 닿아도 감지되지 않습니다.");
        else if (!ownCollider.isTrigger)
            Debug.LogWarning("[PurpleLight] Collider2D의 Is Trigger가 꺼져 있습니다.");
    }

    void OnEnable()
    {
        allOrbs.RemoveAll(orb => orb == null);

        // 씬에 구슬이 하나도 없던 상태에서 처음 켜지는 구슬 = 새 씬(또는 씬 재시작) → 이동권 초기화
        if (allOrbs.Count == 0) hasChargeShared = false;

        if (!allOrbs.Contains(this)) allOrbs.Add(this);
    }

    void OnDisable()
    {
        allOrbs.Remove(this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // 꿈 세계에 있는 동안은 획득 불가
        PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null && pm.isInDream) return;

        // [변경] 이미 이동권이 있어도 구슬은 먹을 수 있음. 다만 이동권은 늘어나지 않고 최대 1개로 유지됨.
        bool alreadyHadCharge = hasChargeShared;
        hasChargeShared = true;

        // 숨기기 전에 먼저 재생 (오디오가 자식 오브젝트에 있어도 아래에서 꺼지지 않도록 처리됨)
        if (pickupAudioSource != null) pickupAudioSource.Play();

        if (!keepVisibleOnPickup) SetEnergyVisible(false);

        Debug.Log(alreadyHadCharge
            ? "[PurpleLight] 구슬 획득 — 이미 이동권이 있어 이동권은 늘어나지 않음 (최대 1개 유지)"
            : "[PurpleLight] 에너지 획득 → 현실→꿈 이동권 1회 획득");
    }

    /// <summary>현실 → 꿈 이동 시 GameManager가 호출. 이동권 소모.</summary>
    public void ConsumeCharge()
    {
        hasChargeShared = false;
    }

    /// <summary>꿈으로 이동한 뒤 GameManager가 호출. 씬의 모든 구슬을 같은 자리에 다시 보이게 함.</summary>
    public void RespawnEnergy()
    {
        for (int i = 0; i < allOrbs.Count; i++)
        {
            if (allOrbs[i] != null) allOrbs[i].SetEnergyVisible(true);
        }
    }

    // 자기 자신을 SetActive(false)하면 스크립트가 같이 멈추므로, 보이는 부분만 끔
    private void SetEnergyVisible(bool visible)
    {
        foreach (Renderer r in ownRenderers) r.enabled = visible;
        if (ownCollider != null) ownCollider.enabled = visible;

        foreach (Transform child in transform)
        {
            // 획득 사운드가 자식 오브젝트에 있다면, 재생이 끊기지 않도록 그 오브젝트는 끄지 않음
            if (pickupAudioSource != null && child.gameObject == pickupAudioSource.gameObject) continue;
            child.gameObject.SetActive(visible);
        }
    }
}