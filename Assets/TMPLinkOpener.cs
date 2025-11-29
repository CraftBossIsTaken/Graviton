using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class TMPLinkOpener : MonoBehaviour, IPointerClickHandler
{
    private TextMeshProUGUI tmpText;

    void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        Debug.Log("[TMPLinkOpener] Awake - component found: " + (tmpText != null));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[TMPLinkOpener] Click detected!");

        if (tmpText == null)
        {
            Debug.LogWarning("[TMPLinkOpener] tmpText is NULL.");
            return;
        }

        Camera cam = eventData.pressEventCamera;
        Debug.Log("[TMPLinkOpener] Camera used: " + cam);

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmpText, Input.mousePosition, cam);

        Debug.Log("[TMPLinkOpener] Link index: " + linkIndex);

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();

            Debug.Log("[TMPLinkOpener] CLICKED LINK: " + linkId);
            Application.OpenURL(linkId);
        }
        else
        {
            Debug.Log("[TMPLinkOpener] No link detected under cursor.");
        }
    }
}
