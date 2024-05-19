using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class SuccessAnimation : MonoBehaviour
{
    private float totalTime = 1f;
    void Start()
    {
        transform.localScale = Vector3.zero;
        var cv = GetComponent<CanvasGroup>();
        cv.alpha = 0;
        
        Sequence s = DOTween.Sequence();
        s.Append(cv.DOFade(1, totalTime));
        s.Join(transform.DORotate(new Vector3(0, 0, -1800), totalTime, RotateMode.FastBeyond360)).SetEase(Ease.OutSine);
        s.Join(transform.DOScale(Vector3.one, totalTime).SetEase(Ease.OutSine));
        s.AppendInterval(1.5f);
        s.Append(cv.DOFade(0, 0.25f));
    }
}
