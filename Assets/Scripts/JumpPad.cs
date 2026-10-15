using UnityEngine;

// 플레이어가 위에서 밟으면 위로 튕겨 올려주는 점프대.
// 스크립트 하나를 여러 오브젝트에 붙이고, 오브젝트마다 Launch Velocity만 다르게 주면
// 높은 점프대/낮은 점프대를 따로 만들 필요 없이 재사용할 수 있습니다.
//
// [Unity 설정]
// 1. 점프대 오브젝트에 Collider2D를 붙이세요. (Is Trigger는 꺼두세요 — 플레이어가 위에 올라서야 합니다)
// 2. 이 스크립트를 붙이고 Launch Velocity를 조절하세요.
//    (높이 ≈ 속도² / (2 × 중력). 일반 점프 jumpForce=8보다 크게 주면 더 높이 뜹니다)
// 3. (선택) Launch Audio Source에 효과음을 연결하세요. Play On Awake / Loop는 꺼두세요.
// 4. [추가] Control Lock Duration으로 발사 직후 좌우 조작이 막히는 시간을 조절하세요.
//
// 꿈/현실 세계에서 중력 방향은 같으므로 어느 세계에 놓아도 똑같이 동작합니다.
[RequireComponent(typeof(Collider2D))]
public class Jumppad : MonoBehaviour
{
    [Header("발사 설정")]
    public float launchVelocity = 14f;  // 위로 튕겨 올라가는 속도

    [Header("발사 직후 조작 잠금")]
    // [추가] 발사 직후 이 시간(초) 동안 좌우 입력이 무시되고 위로 곧게 올라갑니다. 0이면 잠금 없음.
    // 가이드: 점프대 위로 올라가는 시간(= 발사 속도 / 중력 가속도)의 30~60% 정도가 무난합니다.
    // 점프대가 높을수록 값을 키우고, 낮을수록 줄이세요. 보통 0.2 ~ 0.4초 사이에서 조절합니다.
    public float controlLockDuration = 0.3f;

    [Header("플레이어 인식")]
    public string playerTag = "Player";

    [Header("위에서 밟았는지 판정")]
    // 플레이어 콜라이더의 맨 아래가 점프대 윗면보다 이 값 이상 낮으면 "옆/아래에서 닿음"으로 보고 무시
    public float topTolerance = 0.2f;

    [Header("중복 발사 방지")]
    public float cooldown = 0.1f;

    [Header("효과음 (선택)")]
    public AudioSource launchAudioSource;

    private Collider2D padCollider;
    private float lastLaunchTime = -999f;

    void Awake()
    {
        padCollider = GetComponent<Collider2D>();

        if (padCollider.isTrigger)
        {
            Debug.LogWarning($"[JumpPad] '{name}'의 Collider2D가 Is Trigger로 설정돼 있어 플레이어가 통과해버립니다. 꺼주세요.");
        }

        if (launchAudioSource == null)
        {
            launchAudioSource = GetComponentInChildren<AudioSource>(true);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;
        if (Time.time - lastLaunchTime < cooldown) return;

        // 옆이나 아래에서 부딪힌 경우는 무시 (위에서 밟았을 때만 발사)
        float playerBottom = collision.collider.bounds.min.y;
        float padTop = padCollider.bounds.max.y;
        if (playerBottom < padTop - topTolerance) return;

        PlayerMovement player = collision.collider.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        lastLaunchTime = Time.time;
        player.Launch(launchVelocity, controlLockDuration);

        if (launchAudioSource != null) launchAudioSource.Play();
    }
}