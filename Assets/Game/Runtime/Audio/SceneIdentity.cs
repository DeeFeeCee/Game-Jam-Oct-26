using UnityEngine;

public class SceneIdentity : MonoBehaviour
{
    public SceneType sceneType;

    [Header("Non-levels don't need these")]
    public Zone zone;
    public byte phase;

    public enum SceneType
    {
        MainMenu,
        Prologue,
        Level,
        Ending
    }

    public enum Zone
    {
        Aquamarine,
        Emerald,
        Heliodor
    }
}