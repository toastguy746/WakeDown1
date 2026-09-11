using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerAudio : MonoBehaviour
{
    public AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // 무한 루프 발소리 재생/정지 함수
    public void SetWalking(bool isWalking)
    {
        if (audioSource == null) return; // 오디오 소스가 없을 때 에러 방지

        if (isWalking)
        {
            // 걷고 있는데 소리가 안 나고 있다면 재생 시작
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            // 멈췄거나 공중에 떴는데 소리가 나고 있다면 정지
            // 단, 착지 소리가 순간적으로 재생 중일 때는 걷기 정지 명령으로 인해 사운드가 끊기지 않게 예외 처리
            if (audioSource.isPlaying && audioSource.loop)
            {
                audioSource.Stop();
            }
        }
    }

    /// <summary>
    /// 착지 소리를 한 번 재생합니다.
    /// </summary>
    /// <param name="isCurrentlyWalking">착지 시점에 걷기 상태였는지 여부.
    /// true면 착지음 재생 후 다시 루프 발소리로 자연스럽게 복귀합니다.</param>
    public void PlayLandingSound(bool isCurrentlyWalking)
    {
        if (audioSource == null) return;

        // 현재 루프 소리가 재생 중이었다면 잠시 멈추고
        audioSource.Stop();

        // 오디오 소스의 loop 성질을 잠시 끄고 한 번만 '쿵' 재생 (발소리와 같은 사운드 사용)
        audioSource.loop = false;
        audioSource.Play();

        // 걷는 도중에 착지했을 때 소리가 완전히 멈추지 않도록,
        // 걷기 상태라면 다음 프레임에서 다시 루프 재생이 자연스럽게 켜집니다.
        if (isCurrentlyWalking)
        {
            audioSource.loop = true;
        }
    }
}