using UnityEngine;

// Attach to any single GameObject in the scene (e.g. the same one as MissionManager).
// Finds every DragIconController and resets them all at once.
public class IconResetManager : MonoBehaviour
{
    public void ResetAllIcons()
    {
        DragIconController[] icons = Object.FindObjectsByType<DragIconController>(FindObjectsSortMode.None);
        foreach (DragIconController icon in icons)
        {
            icon.ResetToOriginal();
        }
    }
}
