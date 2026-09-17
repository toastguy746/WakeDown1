using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D 사용을 위해 필요

public class ep1Manager : MonoBehaviour
{
    [Header("스팟 라이트 연동")]
    public Light2D spotLight; // 밝기를 올릴 Spot Light 2D

    [Header("페이드인 설정")]
    public float startIntensity = 0.01f; // 시작 밝기
    public float targetIntensity = 1.5f; // 도달할 밝기
    public float fadeDuration = 2f;      // [요청] 밝기가 올라가는 데 걸리는 시간(초), 퍼블릭

    void Start()
    {
        if (spotLight != null)
        {
            spotLight.intensity = startIntensity;
            StartCoroutine(FadeInLight());
        }
    }

    void Update()
    {

    }

    IEnumerator FadeInLight()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            spotLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            yield return null;
        }

        spotLight.intensity = targetIntensity;
    }
}