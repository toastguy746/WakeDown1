using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D 사용을 위해 필요

public class ep1Manager : MonoBehaviour
{
    [Header("스팟 라이트 연동")]
    public Light2D spotLight; // 밝기를 제어할 Spot Light 2D

    [Header("초기 페이드인 설정")]
    public float startIntensity = 0.01f; // 시작 밝기
    public float targetIntensity = 1.5f; // 처음 도달할 밝기
    public float fadeDuration = 2f;      // 밝기가 올라가는 데 걸리는 시간(초)

    [Header("페이드인 이후 왕복(호흡 효과) 설정")]
    // [추가] 처음 fadeDuration에 걸쳐 targetIntensity까지 오른 뒤,
    // oscillateMin ~ oscillateMax 사이를 계속 부드럽게 왕복함
    public float oscillateMin = 1f;
    public float oscillateMax = 1.5f;
    public float oscillateSpeed = 1f; // 값이 클수록 왕복이 빨라짐

    private Coroutine fadeAndOscillateCoroutine; // [추가] 외부(LullabyDialogue)에서 멈출 수 있도록 참조 저장

    void Start()
    {
        if (spotLight != null)
        {
            spotLight.intensity = startIntensity;
            fadeAndOscillateCoroutine = StartCoroutine(FadeInThenOscillate());
        }
    }

    void Update()
    {

    }

    /// <summary>
    /// 왕복(호흡) 효과를 멈춥니다. 자장가가 시작되며 라이트를 0으로 낮춰야 할 때처럼,
    /// 외부에서 라이트 값을 직접 제어하고 싶을 때 이 함수를 먼저 호출해서 이 스크립트가
    /// 더 이상 intensity를 덮어쓰지 않도록 하세요.
    /// </summary>
    public void StopIntensityControl()
    {
        if (fadeAndOscillateCoroutine != null)
        {
            StopCoroutine(fadeAndOscillateCoroutine);
            fadeAndOscillateCoroutine = null;
        }
    }

    IEnumerator FadeInThenOscillate()
    {
        // 1. 0.01 → 1.5까지 fadeDuration에 걸쳐 상승
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            spotLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            yield return null;
        }
        spotLight.intensity = targetIntensity;

        // 2. [추가] 이후로는 계속 oscillateMin ~ oscillateMax 사이를 부드럽게 왕복 (사인파 기반)
        while (true)
        {
            float wave = (Mathf.Sin(Time.time * oscillateSpeed) + 1f) * 0.5f; // 0~1 사이를 부드럽게 왕복
            spotLight.intensity = Mathf.Lerp(oscillateMin, oscillateMax, wave);
            yield return null;
        }
    }
}