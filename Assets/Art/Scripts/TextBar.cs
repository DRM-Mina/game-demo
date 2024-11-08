using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class TextBar : MonoBehaviour
{
    public TextMeshProUGUI text;
    public TextMeshProUGUI timerText;
    public CanvasGroup canvasGroup;
    public RectTransform bg;
    public float moveTime = 0.2f;
    public GameObject BLOCKER;
    public GameObject button;
    public GameObject success;

    private Coroutine timerCoroutine;
    private bool timerStarted = false;

    public void UpdateText(string str)
    {
        var rt = text.rectTransform;
        var s = DOTween.Sequence();
        s.Append(rt.DOAnchorPos(new Vector2(rt.anchoredPosition.x, -rt.rect.height / 2), moveTime)
            .SetEase(Ease.OutSine));
        s.Join(DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 0.0f, moveTime / 2)
            .SetEase(Ease.OutQuart));
        s.AppendCallback(() =>
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.rect.height / 2);
            text.text = str;
        });
        s.Append(rt.DOAnchorPos(new Vector2(rt.anchoredPosition.x, 0), moveTime)
            .SetEase(Ease.OutSine));
        s.Join(DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 1.0f, moveTime / 2)
            .SetEase(Ease.InQuart));
    }

    public void StartTimer(int seconds)
    {
        timerStarted = true;
        var initial = bg.sizeDelta;
        var target = new Vector2(initial.x + 100, initial.y);
        bg.DOSizeDelta(target, 0.5f).OnComplete(() => timerCoroutine = StartCoroutine(Countdown(seconds)));
    }

    private IEnumerator Countdown(int seconds)
    {
        while (seconds > 0)
        {
            timerText.text = seconds + "s";
            yield return new WaitForSeconds(1);
            seconds--;
        }

        EndTimer();
    }

    public void EndTimer()
    {
        if (!timerStarted)
            return;
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        timerText.text = "";
        var initial = bg.sizeDelta;
        var target = new Vector2(initial.x - 100, initial.y);
        bg.DOSizeDelta(target, 0.5f);
        timerStarted = false;
    }

    public void Terminate(string error = "Authentication error. " +
                                        "Please check your internet connection and ensure " +
                                        "that you have actually purchased the game.")
    {
        EndTimer();
        timerText.text = "";
        FindObjectOfType<Authenticator>().isDead = true;
        BLOCKER.SetActive(true);
        var cg = BLOCKER.GetComponent<CanvasGroup>();
        DOTween.To(() => cg.alpha, x => cg.alpha = x, 1, 0.5f);
        text.rectTransform.DOSizeDelta(text.rectTransform.sizeDelta + 100 * Vector2.one, 0.5f);
        bg.DOSizeDelta(bg.sizeDelta + 100 * Vector2.one, 0.5f)
            .OnComplete(()=>
            {
                UpdateText(error);
                button.SetActive(true);
            });
    }

    public void Success()
    {
        UpdateText("GREAT SUCCESS!");
        success.SetActive(true);
    }
}
