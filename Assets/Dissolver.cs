using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class Dissolver : MonoBehaviour
{
    public Material dissolveMaterial; // Assign this in the inspector
    private Coroutine dissolveCoroutine;
    private Dictionary<GameObject, Material[]> originalMaterialsMap = new Dictionary<GameObject, Material[]>();

    public void AnimateDissolve(bool dissolve, GameObject targetObject, float dissolveTime)
    {
        Renderer renderer = targetObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("No renderer found on the target object.");
            return;
        }

        // Store original materials
        if (!originalMaterialsMap.ContainsKey(targetObject))
        {
            originalMaterialsMap[targetObject] = renderer.materials;
        }

        // Create dissolve materials
        Material[] newMaterials = new Material[originalMaterialsMap[targetObject].Length];
        for (int i = 0; i < newMaterials.Length; i++)
        {
            Material originalMaterial = originalMaterialsMap[targetObject][i];
            newMaterials[i] = new Material(dissolveMaterial);

            newMaterials[i].SetTexture("_MainTex", originalMaterial.mainTexture);
            newMaterials[i].SetColor("_Color", originalMaterial.color);

            if (originalMaterial.HasProperty("_EmissionColor"))
                newMaterials[i].SetColor("_EmissionColor", originalMaterial.GetColor("_EmissionColor"));
        }

        renderer.materials = newMaterials;

        // Stop any existing dissolve coroutine
        if (dissolveCoroutine != null)
            StopCoroutine(dissolveCoroutine);

        dissolveCoroutine = StartCoroutine(HandleDissolveEffect(dissolve, newMaterials, dissolveTime, targetObject));
    }

    private IEnumerator HandleDissolveEffect(bool dissolve, Material[] materials, float dissolveTime, GameObject targetObject)
    {
        float start = dissolve ? 1f : 0f;
        float end = dissolve ? 0f : 1f;
        float elapsed = 0f;

        while (elapsed < dissolveTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveTime);
            float value = Mathf.Lerp(start, end, t);

            foreach (Material mat in materials)
                mat.SetFloat("_DissolveAmount", value);

            yield return null;
        }

        foreach (Material mat in materials)
            mat.SetFloat("_DissolveAmount", end);

        if (!dissolve && originalMaterialsMap.TryGetValue(targetObject, out Material[] originalMaterials))
        {
            targetObject.GetComponent<Renderer>().materials = originalMaterials;
        }

        targetObject.SetActive(dissolve ? false : true);
        Destroy(targetObject.transform.root.gameObject);
    }
}
