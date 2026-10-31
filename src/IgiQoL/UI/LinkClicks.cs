using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IgiQoL.UI;

// Opens <link="url"> spans of the TMP label on the same object in the system browser.
public sealed class LinkClicks : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        var text = GetComponent<TMP_Text>();
        if (text == null) return;
        int i = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (i < 0) return;
        string url = text.textInfo.linkInfo[i].GetLinkID();
        if (url.StartsWith("http://") || url.StartsWith("https://")) Application.OpenURL(url);
    }
}
