using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Robust credit roll controller that creates clickable, properly-sized link areas for
/// TextMeshPro <link=> tags. Buttons are created at runtime (no prefab required),
/// handle multi-line links and resizing, and update while the credits scroll.
/// </summary>
public class CreditRollController : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup fadeGroup;
    public RectTransform creditsPanel; // parent for the text and the generated buttons
    public TextMeshProUGUI creditsText;
    public CanvasGroup cat;

    [Header("Settings")]
    public float fadeDuration = 1f;
    public float scrollSpeed = 50f;
    public KeyCode skipKey = KeyCode.Space;

    // Internal
    private bool skipping;
    private Coroutine runningRoutine;

    // Data for the currently created link hit areas.
    private class LinkInfoRuntime
    {
        public string url;
        public int firstCharIndex;
        public int length;
        public List<Button> buttons = new List<Button>(); // one button per continuous segment (usually per line)
    }

    private List<LinkInfoRuntime> activeLinks = new List<LinkInfoRuntime>();

    // Regex to match <link=...> or <link="..."> tags and capture inner text
    private static readonly Regex linkRegex = new Regex("<link=\\\"?(.*?)\\\"?>(.*?)</link>", RegexOptions.Singleline);

    #region Public API
    public void PlayCredits(string text)
    {
        // Make link tags TMP-friendly by ensuring quotes around the id so TMP registers them as links.
        // We also add visual styling (color + underline) inside the link tag.
 string processed = Regex.Replace(text,
    @"<link=(.*?)>(.*?)</link>",
    "<link=$1><color=#1E90FF><u underlineColor=#00A8FF>$2</u></color></link>");

        creditsText.text = processed;

        // Ensure internal layout is up-to-date before creating hit areas
        creditsText.ForceMeshUpdate();

        CreateOrUpdateLinkButtons();

        if (runningRoutine != null) StopCoroutine(runningRoutine);
        runningRoutine = StartCoroutine(CreditsRoutine());
    }
    #endregion

    #region Credits Flow
    private IEnumerator CreditsRoutine()
    {
        skipping = false;
        // Start below screen
        creditsPanel.anchoredPosition = new Vector2(creditsPanel.anchoredPosition.x, -Screen.height);
        yield return StartCoroutine(Fade(0f, 1f));

        // Scroll until skip
        while (!skipping)
        {
            creditsPanel.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;
            if (Input.GetKeyDown(skipKey)) skipping = true;

            // Keep link buttons positions up-to-date in case layout changed (resize or font adjustments)
            if (activeLinks.Count > 0)
                UpdateLinkButtonPositions();

            yield return null;
        }

        yield return StartCoroutine(Fade(1f, 0f));

        // Cleanup
        creditsText.text = "";
        DestroyAllLinkButtons();
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            if (Input.GetKeyDown(skipKey))
            {
                skipping = true;
                break;
            }

            t += Time.deltaTime;
            float value = Mathf.Lerp(from, to, t / fadeDuration);
            if (fadeGroup != null) fadeGroup.alpha = value;
            if (cat != null) cat.alpha = 1f - value;
            yield return null;
        }
        if (fadeGroup != null) fadeGroup.alpha = to;
    }
    #endregion

    #region Link Button Creation & Updates

    private void CreateOrUpdateLinkButtons()
    {
        // Destroy and recreate for correctness
        DestroyAllLinkButtons();
        activeLinks.Clear();

        creditsText.ForceMeshUpdate();
        TMP_TextInfo textInfo = creditsText.textInfo;

        if (textInfo == null || textInfo.characterCount == 0) return;

        // Preferred path: if TMP parsed link tags, use textInfo.linkInfo (most reliable)
        int tmpLinkCount = textInfo.linkCount;
        if (tmpLinkCount > 0)
        {
            for (int i = 0; i < tmpLinkCount; i++)
            {
                var li = textInfo.linkInfo[i];
                string linkId = li.GetLinkID();         // url or id inside link=""
                string linkText = li.GetLinkText();     // visible text
                int firstChar = li.linkTextfirstCharacterIndex;
                int length = li.linkTextLength;

                AddLinkRuntime(linkId, firstChar, length, textInfo);
            }
        }
        else
        {
            // Fallback: parse using regex and map against visible characters (stripping tags)
            string raw = creditsText.text;
            if (string.IsNullOrEmpty(raw)) return;

            MatchCollection matches = linkRegex.Matches(raw);
            if (matches.Count == 0) return;

            // Build "visible" string from characterInfo (it contains only rendered characters, tags are excluded)
            string visible = BuildVisibleString(textInfo);

            int searchOffset = 0;
            foreach (Match match in matches)
            {
                string url = match.Groups[1].Value;
                string innerRaw = match.Groups[2].Value;
                // strip any inner tags (color/underline) to get actual visible text
                string innerVisible = Regex.Replace(innerRaw, "<.*?>", "");

                int startIndex = visible.IndexOf(innerVisible, searchOffset);
                if (startIndex == -1)
                {
                    // couldn't find; skip this one
                    continue;
                }
                searchOffset = startIndex + innerVisible.Length;

                AddLinkRuntime(url, startIndex, innerVisible.Length, textInfo);
            }
        }
    }

    // Helper that creates buttons and adds the LinkInfoRuntime entry
    private void AddLinkRuntime(string url, int firstCharIndex, int length, TMP_TextInfo textInfo)
    {
        // Ensure character data exists for this range
        if (firstCharIndex >= textInfo.characterCount) return;

        LinkInfoRuntime lir = new LinkInfoRuntime
        {
            url = url,
            firstCharIndex = firstCharIndex,
            length = length
        };

        var segments = GetCharacterSegments(textInfo, firstCharIndex, length);

        foreach (var seg in segments)
        {
            Button btn = CreateButtonUnderPanel("LinkButton", creditsPanel);
            RectTransform rt = btn.GetComponent<RectTransform>();

            // Set anchor/pivot to center so anchoredPosition maps to local panel position
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 size = seg.size;
            Vector2 center = seg.center;

            rt.anchoredPosition = center;
            rt.sizeDelta = size;

            // Put the button above the text in hierarchy so it receives raycasts (and doesn't get occluded)
            int textSibling = creditsText.transform.GetSiblingIndex();
            rt.SetSiblingIndex(Mathf.Min(creditsPanel.childCount - 1, textSibling + 1));

            string capturedUrl = url; // capture for closure
            btn.onClick.AddListener(() => OnLinkClicked(capturedUrl));

            lir.buttons.Add(btn);
        }

        activeLinks.Add(lir);
    }

    // Update positions (recalculate rectangles) - called while credits are active
    private void UpdateLinkButtonPositions()
    {
        creditsText.ForceMeshUpdate();
        TMP_TextInfo textInfo = creditsText.textInfo;

        // Recompute segments for each link and update corresponding buttons. If counts mismatch, recreate everything.
        int totalSegmentCount = 0;
        List<List<RectTransformData>> newData = new List<List<RectTransformData>>();

        foreach (var link in activeLinks)
        {
            var segs = GetCharacterSegments(textInfo, link.firstCharIndex, link.length);
            totalSegmentCount += segs.Count;
            newData.Add(segs);
        }

        // Count existing buttons
        int existingCount = 0;
        foreach (var l in activeLinks) existingCount += l.buttons.Count;

        if (existingCount != totalSegmentCount)
        {
            // Layout changed (wraps/resize). Recreate cleanly for correctness.
            CreateOrUpdateLinkButtons();
            return;
        }

        // Update transforms in the same order as created
        for (int i = 0; i < activeLinks.Count; i++)
        {
            var link = activeLinks[i];
            var segs = newData[i];
            for (int s = 0; s < segs.Count; s++)
            {
                var rectData = segs[s];
                Button btn = link.buttons[s];
                if (btn == null) continue;
                RectTransform rt = btn.GetComponent<RectTransform>();
                rt.anchoredPosition = rectData.center;
                rt.sizeDelta = rectData.size;
            }
        }
    }

    private void DestroyAllLinkButtons()
    {
        foreach (var link in activeLinks)
        {
            foreach (var b in link.buttons)
            {
                if (b != null) Destroy(b.gameObject);
            }
            link.buttons.Clear();
        }
    }

    private void OnLinkClicked(string url)
    {
        // Basic behavior: open URL
        Application.OpenURL(url);
    }

    #endregion

    #region Helpers for creating UI and computing rectangles

    // Small helper structure to return computed rect data in panel local space
    private struct RectTransformData
    {
        public Vector2 center;
        public Vector2 size;
    }

    // Create a basic transparent Button GameObject under the creditsPanel
    private Button CreateButtonUnderPanel(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.raycastTarget = true;
        // Make it invisible but still a hit target
        img.color = new Color(0f, 0f, 0f, 0f);

        Button btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;

        return btn;
    }

    // Build a plain "visible" string directly from characterInfo (excludes tags)
    private string BuildVisibleString(TMP_TextInfo textInfo)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var ci = textInfo.characterInfo[i];
            sb.Append(ci.character);
        }
        return sb.ToString();
    }

    // Compute continuous character segments for a given range, and return their centers & sizes in panel local space
    private List<RectTransformData> GetCharacterSegments(TMP_TextInfo textInfo, int startCharIndex, int length)
    {
        List<RectTransformData> results = new List<RectTransformData>();
        if (textInfo == null || textInfo.characterCount == 0) return results;

        int endChar = Mathf.Min(startCharIndex + length, textInfo.characterCount);

        int currentLine = -1;
        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
        bool inSegment = false;

        for (int i = startCharIndex; i < endChar; i++)
        {
            TMP_CharacterInfo ci = textInfo.characterInfo[i];
            // If character is not visible (space), still include it as zero-width so segments remain continuous.
            int line = ci.lineNumber;
            Vector3 bl = ci.bottomLeft;
            Vector3 tr = ci.topRight;

            if (!inSegment)
            {
                inSegment = true;
                currentLine = line;
                minX = bl.x;
                maxX = tr.x;
                minY = bl.y;
                maxY = tr.y;
            }
            else if (line != currentLine)
            {
                // flush previous segment
                results.Add(ComputePanelRect(textInfo, minX, minY, maxX, maxY));

                // start new
                currentLine = line;
                minX = bl.x;
                maxX = tr.x;
                minY = bl.y;
                maxY = tr.y;
            }
            else
            {
                // extend on same line
                if (bl.x < minX) minX = bl.x;
                if (tr.x > maxX) maxX = tr.x;
                if (bl.y < minY) minY = bl.y;
                if (tr.y > maxY) maxY = tr.y;
            }
        }

        if (inSegment)
        {
            results.Add(ComputePanelRect(textInfo, minX, minY, maxX, maxY));
        }

        return results;
    }

    // Convert a rectangle defined in the text object's local space (x/y from character info) to panel-local centered rect data
    private RectTransformData ComputePanelRect(TMP_TextInfo textInfo, float minX, float minY, float maxX, float maxY)
    {
        Vector3 localBL = new Vector3(minX, minY, 0f);
        Vector3 localTR = new Vector3(maxX, maxY, 0f);

        Transform textTransform = creditsText.transform;
        Vector3 worldBL = textTransform.TransformPoint(localBL);
        Vector3 worldTR = textTransform.TransformPoint(localTR);

        Vector3 panelBL = creditsPanel.InverseTransformPoint(worldBL);
        Vector3 panelTR = creditsPanel.InverseTransformPoint(worldTR);

        Vector2 size = panelTR - panelBL;
        Vector2 center = (Vector2)panelBL + size / 2f;

        size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));

        return new RectTransformData { center = center, size = size };
    }

    #endregion
}
