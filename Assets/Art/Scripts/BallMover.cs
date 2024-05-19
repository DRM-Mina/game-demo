using DG.Tweening;
using UnityEngine;

public class BallMover : MonoBehaviour
{
    private Authenticator auth;
    private Tween tw;
    void Start()
    {
        auth = FindObjectOfType<Authenticator>();
        transform.position = new Vector3(5, 0, 0);
        Sequence s = DOTween.Sequence();
        //move ball from right to left
        s.Append(transform.DOMoveX(-5, 2).SetEase(Ease.InOutSine));
        //move ball from left to right
        s.Append(transform.DOMoveX(5, 2).SetEase(Ease.InOutSine));
        //loop the sequence
        s.SetLoops(-1, LoopType.Yoyo);
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !auth.isDead)
        {
            tw?.Complete();
            tw = transform.DOPunchScale(Vector3.one * 1, 0.6f, 5, 0.5f);
        }
    }
        
}
