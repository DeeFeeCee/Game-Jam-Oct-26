using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneIdentity : MonoBehaviour
{
    public SceneType SceneId;

    [Header("Non-levels don't need these")]
    public ZoneType Zone;
    [Range(1, 4)] public byte PhaseId = 1;

    public enum SceneType { MainMenu, Prologue, Level, Ending }
    public enum ZoneType { Aquamarine, Emerald, Heliodor }

    private static bool hasPreviousMusicDestination;
    private static SceneType previousScene;
    private static ZoneType previousZone;
    private static byte previousPhase;
    private static string previousSceneName;
    private static AudioManager previousManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hasPreviousMusicDestination = false;
        previousSceneName = null;
        previousManager = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Awake()
    {
        /* sceneLoaded runs after Awake. This subscription also handles play
        sessions that begin with this scene already loaded before the
        runtime initialization callback */
        if (AudioManager.Instance != null && !hasPreviousMusicDestination)
            OnSceneLoaded(gameObject.scene, LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneIdentity destination = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            destination = root.GetComponentInChildren<SceneIdentity>(true);
            if (destination != null) break;
        }

        // Scenes without an identity (e.g. test fixtures) do not alter music
        if (destination == null || AudioManager.Instance == null) return;

        bool retry = hasPreviousMusicDestination && previousManager == AudioManager.Instance &&
            previousScene == destination.SceneId &&
            previousZone == destination.Zone &&
            previousPhase == destination.PhaseId &&
            previousSceneName == scene.name;
        previousScene = destination.SceneId;
        previousZone = destination.Zone;
        previousPhase = destination.PhaseId;
        previousSceneName = scene.name;
        previousManager = AudioManager.Instance;
        hasPreviousMusicDestination = true;

        /* The prologue uses menu music: retain it when already active, & replace
        any prior level music when returning to the prologue for a new run */
        if (destination.SceneId == SceneType.Prologue)
        {
            AudioManager.Instance.EnsurePrologueMusic();
            return;
        }

        if (!retry)
            AudioManager.Instance.PlaySceneMusicAfterFade(destination.SceneId, destination.Zone, destination.PhaseId);
    }
}