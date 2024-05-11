using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class LoadingSpinner : MonoBehaviour
{
    private Sequence _sequence;
    public float rotateSpeed = 30f;
    void Start()
    {
        _sequence = DOTween.Sequence();
        _sequence.Append(
            transform.DORotate(new Vector3(0, 0, -360), 360f/rotateSpeed, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                ).SetLoops(-1);
    }
}
