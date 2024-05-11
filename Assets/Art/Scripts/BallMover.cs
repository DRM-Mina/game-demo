using DG.Tweening;
using UnityEngine;

public class BallMover : MonoBehaviour
{
    void Start()
    {
        transform.position = new Vector3(5, 0, 0);
        Sequence s = DOTween.Sequence();
        //move ball from right to left
        s.Append(transform.DOMoveX(-5, 2).SetEase(Ease.InOutSine));
        //move ball from left to right
        s.Append(transform.DOMoveX(5, 2).SetEase(Ease.InOutSine));
        //loop the sequence
        s.SetLoops(-1, LoopType.Yoyo);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
