using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IgiQoL.UI;

// Moves target by the drag delta (as FleetCargoSection.OnDrag). Needs a raycast-target Graphic on the same object.
// A drag never counts as a click, so a button can carry its own DragHandle.
public sealed class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform target;
    public Action<Vector2> onEndDrag;

    public void OnBeginDrag(PointerEventData eventData) => eventData.eligibleForClick = false;

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;
        target.anchoredPosition += eventData.delta / target.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (target != null) onEndDrag?.Invoke(target.anchoredPosition);
    }
}
