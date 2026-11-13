using System.Collections;
using GameJam.UI;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private Coroutine musicTransition;
    private bool hasActiveLevelMusic;
    private SceneIdentity.SceneType? pendingMusicScene;
    private bool pauseMenuOpen;
    private bool isPauseMusicActive;
    private SceneIdentity.ZoneType activeLevelZone;
    private byte activeLevelPhase;
    private const float MusicLoadTimeoutSeconds = 10f;
    private const float MusicTransitionSeconds = 0.571429f; // Half note
    private const float PauseMusicTransitionSeconds = 0.071429f; // 32nd note
    private const float EmergeJingleDurationSeconds = 4.571429f; // 8 Quarters

    [Header("----- Audio Mixer -----")]
    [SerializeField] private AudioMixer AudioMixer;

    [Header("----- Audio Source -----")]
    public AudioSource PrimaryMusicSource;
    public AudioSource Phase1Source;
    public AudioSource Phase2Source;
    public AudioSource Phase3Source;
    public AudioSource Phase4Source;
    public AudioSource SfxSource;

    [Header("----- Audio Clips -----")]
    // Music
    public AudioClip MainMenu;
    public AudioClip AquamarinePause;
    public AudioClip AquamarinePhase1;
    public AudioClip AquamarinePhase2;
    public AudioClip AquamarinePhase3;
    public AudioClip AquamarinePhase4;
    public AudioClip EmeraldPause;
    public AudioClip EmeraldPhase1;
    public AudioClip EmeraldPhase2;
    public AudioClip EmeraldPhase3;
    public AudioClip EmeraldPhase4;
    public AudioClip HeliodorPause;
    public AudioClip HeliodorPhase1;
    public AudioClip HeliodorPhase2;
    public AudioClip HeliodorPhase3;
    public AudioClip HeliodorPhase4;
    public AudioClip EmergeJingle;
    public AudioClip Emergence;
    // SFX

    /* If you change or remove any sound files (music or sfx),
    please change or remove the above clips appropriately */

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PauseController.PauseStateChanged += OnPauseStateChanged;
    }

    private void OnDestroy()
    {
        PauseController.PauseStateChanged -= OnPauseStateChanged;
        if (Instance == this) Instance = null;
    }

    /*  // If we get around to adding settings
    private void Start()
    {
        // Load volume preferences - Default value is 0.5f
        float musicVolume = PlayerPrefs.GetFloat("musicVolume", 0.2f);
        float sfxVolume = PlayerPrefs.GetFloat("sfxVolume", 0.5f);
        AudioMixer.SetFloat("musicVolume", Mathf.Log10(musicVolume) * 20);
        AudioMixer.SetFloat("sfxVolume", Mathf.Log10(sfxVolume) * 20);
    }
    */

    public static IEnumerator FadeOutMusic(AudioSource source, float fadeSeconds)
    {
        if (source == null) yield break;
        float startVolume = source.volume;
        if (!source.isPlaying || startVolume <= 0f)
        {
            source.Stop();
            yield break;
        }

        if (fadeSeconds <= 0f)
            source.volume = 0f;
        else
        {
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / fadeSeconds);
                /* Ease the dB ramp so it holds its level early, then reaches
                near-silence only at the end instead of spending much of the
                fade below audibility */
                float easedTime = normalizedTime * normalizedTime;
                float attenuation = Mathf.Pow(10f, -48f * easedTime / 20f);
                source.volume = startVolume * attenuation;
                yield return null;
            }
        }

        source.volume = 0f;
        source.Stop();
        source.volume = startVolume;
    }

    public IEnumerator FadeOutAllMusic(float fadeSeconds)
    {
        Coroutine[] fades =
        {
            StartCoroutine(FadeOutMusic(PrimaryMusicSource, fadeSeconds)),
            StartCoroutine(FadeOutMusic(Phase1Source, fadeSeconds)),
            StartCoroutine(FadeOutMusic(Phase2Source, fadeSeconds)),
            StartCoroutine(FadeOutMusic(Phase3Source, fadeSeconds)),
            StartCoroutine(FadeOutMusic(Phase4Source, fadeSeconds))
        };

        foreach (Coroutine fade in fades)
            yield return fade;
    }

    public void PlaySceneMusicAfterFade(SceneIdentity.SceneType scene,
    SceneIdentity.ZoneType zone, byte phase)
    {
        /* Prologue currently shares the menu track, so we shouldn't fade
        or restart even if its scene identity reaches this dispatcher */
        if (scene == SceneIdentity.SceneType.Prologue)
        {
            EnsurePrologueMusic();
            return;
        }

        /* Avoid restarting the menu track when a transition is routed as MainMenu
        while that exact track is already playing (including menu -> prologue) */
        if (scene == SceneIdentity.SceneType.MainMenu &&
            PrimaryMusicSource != null && PrimaryMusicSource.clip == MainMenu
            && PrimaryMusicSource.isPlaying && !pendingMusicScene.HasValue)
            return;

        if (scene == SceneIdentity.SceneType.Level && !pendingMusicScene.HasValue &&
            hasActiveLevelMusic && activeLevelZone == zone)
        {
            if (activeLevelPhase == phase) return;
            StartMusicTransition(CrossfadeLevelPhase(activeLevelPhase, phase, MusicTransitionSeconds));
            activeLevelPhase = phase;
            return;
        }

        StartMusicTransition(PlaySceneMusicAfterFadeRoutine(scene, zone, phase), scene);
    }

    private void StartMusicTransition(IEnumerator transition, SceneIdentity.SceneType? destination = null)
    {
        // The manager owns the child fade coroutines as well as the transition.
        // Cancel both when a newer scene or pause request supersedes the old one.
        if (musicTransition != null)
            StopAllCoroutines();
        pendingMusicScene = destination;
        musicTransition = StartCoroutine(transition);
    }

    private IEnumerator PlaySceneMusicAfterFadeRoutine(SceneIdentity.SceneType scene,
    SceneIdentity.ZoneType zone, byte phase)
    {
        AudioClip[] clips = GetDestinationClips(scene, zone);
        yield return PrepareMusic(clips);
        pendingMusicScene = null;
        foreach (AudioClip clip in clips)
        {
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogError($"Could not load music for {scene}: {clip?.name ?? "missing clip"}.", this);
                yield break;
            }
        }

        // Keep the outgoing music playing while the new destination prepares.
        // A fast pause/resume can update its intent without canceling this fade.
        pendingMusicScene = scene;
        yield return FadeOutAllMusic(MusicTransitionSeconds);
        pendingMusicScene = null;

        switch (scene)
        {
            case SceneIdentity.SceneType.MainMenu:
            case SceneIdentity.SceneType.Prologue:
                PlayMainMenuMusic();
                break;
            case SceneIdentity.SceneType.Ending:
                yield return PlayEndingMusicSequence();
                break;
            case SceneIdentity.SceneType.Level:
                switch (zone)
                {
                    case SceneIdentity.ZoneType.Aquamarine: StartZone1Music();
                    break;
                    case SceneIdentity.ZoneType.Emerald: StartZone2Music();
                    break;
                    case SceneIdentity.ZoneType.Heliodor: StartZone3Music();
                    break;
                }
                SetLevelMusicPhase(phase);
                hasActiveLevelMusic = true;
                activeLevelZone = zone;
                activeLevelPhase = phase;
                isPauseMusicActive = false;
                if (pauseMenuOpen)
                {
                    isPauseMusicActive = true;
                    yield return CrossfadePauseMusic(true);
                }
                break;
            default:
                hasActiveLevelMusic = false;
                isPauseMusicActive = false;
                break;
        }
    }

    void StartSound(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null) return;
        source.volume = volume;
        if (source.clip != clip)
        {
            source.clip = clip;
            source.Play();
        }
        else if (!source.isPlaying)
        {
            source.Play();
        }
    }

    public void PlayMainMenuMusic()
    {
        pauseMenuOpen = false;
        isPauseMusicActive = false;
        hasActiveLevelMusic = false;
        if (PrimaryMusicSource != null) PrimaryMusicSource.loop = true;
        StopLevelMusic();
        StartSound(PrimaryMusicSource, MainMenu, 1f);
    }

    public void EnsurePrologueMusic()
    {
        if (pendingMusicScene == SceneIdentity.SceneType.MainMenu) return;
        PlaySceneMusicAfterFade(SceneIdentity.SceneType.MainMenu, default, 0);
    }

    private AudioClip[] GetDestinationClips(SceneIdentity.SceneType scene, SceneIdentity.ZoneType zone)
    {
        if (scene == SceneIdentity.SceneType.MainMenu)
            return new[] { MainMenu };
        if (scene == SceneIdentity.SceneType.Ending)
            return new[] { EmergeJingle, Emergence };

        return zone switch
        {
            SceneIdentity.ZoneType.Aquamarine => new[] {
                AquamarinePause, AquamarinePhase1, AquamarinePhase2, AquamarinePhase3, AquamarinePhase4 },
            SceneIdentity.ZoneType.Emerald => new[] {
                EmeraldPause, EmeraldPhase1, EmeraldPhase2, EmeraldPhase3, EmeraldPhase4 },
            SceneIdentity.ZoneType.Heliodor => new[] {
                HeliodorPause, HeliodorPhase1, HeliodorPhase2, HeliodorPhase3, HeliodorPhase4 },
            _ => System.Array.Empty<AudioClip>()
        };
    }

    private static IEnumerator PrepareMusic(AudioClip[] clips)
    {
        foreach (AudioClip clip in clips)
        {
            if (clip != null && clip.loadInBackground && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }

        double deadline = Time.realtimeSinceStartupAsDouble + MusicLoadTimeoutSeconds;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            bool loading = false;
            foreach (AudioClip clip in clips)
                loading |= clip != null && clip.loadState == AudioDataLoadState.Loading;
            if (!loading) yield break;
            yield return null;
        }
    }

    public void StartZone1Music() => StartZoneMusic(AquamarinePause,
    AquamarinePhase1, AquamarinePhase2, AquamarinePhase3, AquamarinePhase4);
    public void StartZone2Music() => StartZoneMusic(EmeraldPause,
    EmeraldPhase1, EmeraldPhase2, EmeraldPhase3, EmeraldPhase4);
    public void StartZone3Music() => StartZoneMusic(HeliodorPause,
    HeliodorPhase1, HeliodorPhase2, HeliodorPhase3, HeliodorPhase4);

    private void StartZoneMusic(AudioClip pause, AudioClip phase1,
    AudioClip phase2, AudioClip phase3, AudioClip phase4)
    {
        if (PrimaryMusicSource != null) PrimaryMusicSource.loop = true;
        StartSound(PrimaryMusicSource, pause, 0f);
        StartSound(Phase1Source, phase1, 0f);
        StartSound(Phase2Source, phase2, 0f);
        StartSound(Phase3Source, phase3, 0f);
        StartSound(Phase4Source, phase4, 0f);
    }

    public void SetLevelMusicPhase(byte phaseId)
    {
        AudioSource[] phases = { Phase1Source, Phase2Source, Phase3Source, Phase4Source };
        for (int i = 0; i < phases.Length; i++)
            if (phases[i] != null) phases[i].volume = phaseId == i + 1 ? 1f : 0f;
    }

    private IEnumerator CrossfadeLevelPhase(byte fromPhase, byte toPhase, float fadeSeconds)
    {
        AudioSource outgoing = GetPhaseSource(fromPhase);
        AudioSource incoming = GetPhaseSource(toPhase);
        if (incoming == null) yield break;
        if (incoming == outgoing)
        {
            incoming.volume = 1f;
            yield break;
        }

        AudioSource[] phases = { Phase1Source, Phase2Source, Phase3Source, Phase4Source };
        foreach (AudioSource phase in phases)
            if (phase != null && phase != outgoing && phase != incoming)
                phase.volume = 0f;

        yield return CrossfadeMusicSources(outgoing, incoming, fadeSeconds);
    }

    private void OnPauseStateChanged(bool paused)
    {
        pauseMenuOpen = paused;
        if (pendingMusicScene.HasValue || !hasActiveLevelMusic || isPauseMusicActive == paused) return;

        isPauseMusicActive = paused;
        StartMusicTransition(CrossfadePauseMusic(paused));
    }

    private IEnumerator CrossfadePauseMusic(bool paused)
    {
        AudioSource phase = GetPhaseSource(activeLevelPhase);
        AudioClip pauseClip = GetPauseMusicClip(activeLevelZone);
        if (PrimaryMusicSource == null || phase == null || pauseClip == null)
            yield break;

        float primaryVolume = PrimaryMusicSource.volume;
        StartSound(PrimaryMusicSource, pauseClip, paused ? 0f : primaryVolume);
        AudioSource outgoing = paused ? phase : PrimaryMusicSource;
        AudioSource incoming = paused ? PrimaryMusicSource : phase;

        AudioSource[] phases = { Phase1Source, Phase2Source, Phase3Source,
        Phase4Source };
        foreach (AudioSource source in phases)
            if (source != null && source != phase) source.volume = 0f;

        yield return CrossfadeMusicSources(outgoing, incoming, PauseMusicTransitionSeconds);
    }

    private IEnumerator CrossfadeMusicSources(AudioSource outgoing,
    AudioSource incoming, float fadeSeconds)
    {
        if (incoming == null) yield break;
        if (incoming == outgoing)
        {
            incoming.volume = 1f;
            yield break;
        }

        float outgoingStartVolume = outgoing != null ? outgoing.volume : 0f;
        if (outgoingStartVolume <= 0f) outgoingStartVolume = 1f;
        float finalVolume = outgoingStartVolume;
        incoming.volume = 0f;
        if (!incoming.isPlaying) incoming.Play();

        if (fadeSeconds <= 0f)
        {
            if (outgoing != null) outgoing.volume = 0f;
            incoming.volume = finalVolume;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSeconds);
            float easedT = t * t;
            /* Ease the incoming track while normalizing the pair so their
            summed gain cannot peak above the outgoing level at the midpoint */
            float outgoingGain = Mathf.Cos(easedT * Mathf.PI * 0.5f);
            float incomingGain = Mathf.Sin(easedT * Mathf.PI * 0.5f);
            float gainSum = outgoingGain + incomingGain;
            if (outgoing != null) outgoing.volume = outgoingStartVolume * outgoingGain / gainSum;
            incoming.volume = outgoingStartVolume * incomingGain / gainSum;
            yield return null;
        }

        if (outgoing != null) outgoing.volume = 0f;
        incoming.volume = finalVolume;
    }

    private AudioClip GetPauseMusicClip(SceneIdentity.ZoneType zone)
        => zone switch
    {
        SceneIdentity.ZoneType.Aquamarine => AquamarinePause,
        SceneIdentity.ZoneType.Emerald => EmeraldPause,
        SceneIdentity.ZoneType.Heliodor => HeliodorPause,
        _ => null
    };

    private AudioSource GetPhaseSource(byte phase) => phase switch
    {
        1 => Phase1Source,
        2 => Phase2Source,
        3 => Phase3Source,
        4 => Phase4Source,
        _ => null
    };

    private void StopLevelMusic()
    {
        AudioSource[] phases = { Phase1Source, Phase2Source, Phase3Source,
        Phase4Source };
        foreach (AudioSource source in phases)
            if (source != null) source.Stop();
    }

    public void PlayEndingMusic() =>
        PlaySceneMusicAfterFade(SceneIdentity.SceneType.Ending, default, 0);

    private IEnumerator PlayEndingMusicSequence()
    {
        pauseMenuOpen = false;
        isPauseMusicActive = false;
        hasActiveLevelMusic = false;
        StopLevelMusic();
        if (PrimaryMusicSource == null || EmergeJingle == null) yield break;

        PrimaryMusicSource.loop = false;
        StartSound(PrimaryMusicSource, EmergeJingle, 1f);
        yield return new WaitForSecondsRealtime(EmergeJingleDurationSeconds);

        if (PrimaryMusicSource == null ||
            PrimaryMusicSource.clip != EmergeJingle || Emergence == null)
            yield break;

        PrimaryMusicSource.loop = true;
        StartSound(PrimaryMusicSource, Emergence, 1f);
    }
    /*
    public void PlaySoundCorrect()
    {
        sfxSource.PlayOneShot(Correct);
    }

    public void PlaySoundIncorrect()
    {
        sfxSource.PlayOneShot(Incorrect);
    }
    */
}