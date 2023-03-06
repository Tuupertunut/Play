using UnityEngine.UIElements;

public static class VisualTreeExtensions
{
    public static VisualElement CloneTreeAndGetFirstChildCached(this VisualTreeAsset visualTreeAsset)
    {
        return VisualElementCacheManager.GetVisualElement(visualTreeAsset);
    }
}
