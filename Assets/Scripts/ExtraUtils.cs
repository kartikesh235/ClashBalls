using System.Collections;
using UnityEngine;

public static class ExtraUtils
{
    public static IEnumerator SetDelay(float delay, System.Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    } 
    
    public static IEnumerator SetValueSmoothAfterADelay(float delay, System.Action<float> action,float startValue ,float endValue, float duration)
    {
        yield return new WaitForSeconds(delay);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            action?.Invoke(Mathf.Lerp(startValue, endValue, elapsed / duration));
            yield return null;
        }
        action?.Invoke(endValue);
    }
    
    public static IEnumerator SetValueSmooth(System.Action<float> action,float startValue ,float endValue, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            action?.Invoke(Mathf.Lerp(startValue, endValue, elapsed / duration));
            yield return null;
        }
        action?.Invoke(endValue);
    }
}