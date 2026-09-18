using System;
using System.Collections;
using UnityEngine;

// 여러 스크립트(ep1Manager, LullabyDialogue, DialogueSlot, WorldTransitionUI 등)에서
// 거의 똑같이 반복되던 "duration 동안 값을 from에서 to로 서서히 바꾸는" 코루틴 패턴을
// 한 곳으로 모은 유틸리티입니다.
//
// static 클래스라 직접 코루틴을 실행할 수 없으므로, 호출하는 쪽 MonoBehaviour에서
// StartCoroutine(FadeUtility.LerpFloat(...))처럼 감싸서 사용합니다.
//
// 예: yield return FadeUtility.LerpFloat(spotLight.intensity, 0f, 2f, v => spotLight.intensity = v);
public static class FadeUtility
{
    public static IEnumerator LerpFloat(float from, float to, float duration, Action<float> setValue)
    {
        if (duration <= 0f)
        {
            setValue(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            setValue(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        setValue(to);
    }
}