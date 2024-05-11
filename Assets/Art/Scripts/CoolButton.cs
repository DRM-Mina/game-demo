using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CoolButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
    IPointerExitHandler
{
    public RectTransform top;
    public Vector2 initialAnchoredPos;

    public bool Interactable
    {
        get => _button.interactable;
        set => _button.interactable = value;
    }
    [Space] 
    public UnityEvent onPointerDown;
    public UnityEvent onPointerUp;

    // gets invoked every frame while pointer is down
    public UnityEvent whilePointerPressed;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        initialAnchoredPos = top.anchoredPosition;
    }

    private IEnumerator WhilePressed()
    {
        // this looks strange but is okey in a Coroutine
        // as long as you yield somewhere
        while (true)
        {
            whilePointerPressed?.Invoke();
            yield return null;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // ignore if button not interactable
        if (!_button.interactable) return;

        // just to be sure kill all current routines
        // (although there should be none)
        StopAllCoroutines();

        onPointerDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopAllCoroutines();
        onPointerUp?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopAllCoroutines();
        onPointerUp?.Invoke();
    }

    // Afaik needed so Pointer exit works .. doing nothing further
    public void OnPointerEnter(PointerEventData eventData)
    {
    }

    public void OnPointerDown()
    {
        top.DOAnchorPos(new Vector2(top.anchoredPosition.x, 0), 0.1f);
    }

    public void OnPointerUp()
    {
        top.DOAnchorPos(initialAnchoredPos, 0.1f);
    }

}