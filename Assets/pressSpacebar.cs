using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class pressSpacebar : MonoBehaviour
{
    private CanvasGroup _canvasGroup;
    private Authenticator auth;
    public GameObject touch;
    private Sequence s;
    private bool isKilled = false;
    void Start()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        auth = FindObjectOfType<Authenticator>();
        //scale loop of touch object
        s = DOTween.Sequence();
        s.Append(touch.transform.DOScale(Vector3.one * 0.8f, 0.5f));
        s.SetLoops(-1);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || auth.isDead)
        {
            if(!isKilled)
            {
                isKilled = true;
                s.Kill();
                _canvasGroup.DOFade(0, 0.1f).OnComplete(
                    () =>
                    {
                        Destroy(gameObject);
                    });
            }
        }
    }
    
}
