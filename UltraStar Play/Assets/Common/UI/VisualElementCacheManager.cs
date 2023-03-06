using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UniInject;
using UniRx;
using UnityEditor.UIElements;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class VisualElementCacheManager : AbstractSingletonBehaviour, INeedInjection
{
	public VisualElementCacheManager Instance => DontDestroyOnLoadManager.Instance.FindComponentOrThrow<VisualElementCacheManager>();

    private static Dictionary<VisualTreeAsset, VisualElementCache> visualTreeAssetToCaches = new();

    protected override object GetInstance()
    {
        return Instance;
    }

    public static VisualElement GetVisualElement(VisualTreeAsset visualTreeAsset)
    {
        if (!visualTreeAssetToCaches.TryGetValue(visualTreeAsset, out VisualElementCache cache))
        {
            cache = new VisualElementCache(visualTreeAsset);
            visualTreeAssetToCaches[visualTreeAsset] = cache;
        }
        return cache.GetOrCreateVisualElement();
    }

    private class VisualElementCache
    {
        private readonly List<VisualElement> freeVisualElements = new();

        private readonly VisualTreeAsset visualTreeAsset;

        public VisualElementCache(VisualTreeAsset visualTreeAsset)
        {
            this.visualTreeAsset = visualTreeAsset;
        }

        public VisualElement GetOrCreateVisualElement()
        {
            if (freeVisualElements.IsNullOrEmpty())
            {
                return CreateVisualElement();
            }
            else
            {
                VisualElement visualElement = freeVisualElements.First();
                Debug.Log($"Reusing VisualElement of {visualTreeAsset.name}: {visualElement}");
                freeVisualElements.Remove(visualElement);
                return visualElement;
            }
        }

        private VisualElement CreateVisualElement()
        {
            Debug.Log($"CreateVisualElement of {visualTreeAsset.name}");
            VisualElement visualElement = visualTreeAsset.CloneTree().Children().FirstOrDefault();
            visualElement.RegisterCallback<DetachFromPanelEvent>(evt => OnDetachFromPanel(visualElement, evt));
            return visualElement;
        }

        private void OnDetachFromPanel(VisualElement visualElement, DetachFromPanelEvent evt)
        {
            Debug.Log($"OnDetachFromPanel of {visualTreeAsset.name}: {visualElement}");
            visualElement.userData = null;
            visualElement.Unbind();
            freeVisualElements.Add(visualElement);
        }
    }
}
