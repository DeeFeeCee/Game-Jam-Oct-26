using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("----- Audio Mixer -----")]
    [SerializeField] private AudioMixer AudioMixer;

    [Header("----- Audio Source -----")]
    [SerializeField] AudioSource PrimaryMusicSource;
    [SerializeField] AudioSource Phase1Source;
    [SerializeField] AudioSource Phase2Source;
    [SerializeField] AudioSource Phase3Source;
    [SerializeField] AudioSource Phase4Source;
    [SerializeField] AudioSource SfxSource;

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
    public AudioClip Emergence;
    // SFX

    // If you change or remove any sound files (music or sfx), please change or remove the above clips appropriately

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Load volume preferences - Default value is 0.5f
        float musicVolume = PlayerPrefs.GetFloat("musicVolume", 0.2f);
        float sfxVolume = PlayerPrefs.GetFloat("sfxVolume", 0.5f);
        AudioMixer.SetFloat("musicVolume", Mathf.Log10(musicVolume) * 20);
        AudioMixer.SetFloat("sfxVolume", Mathf.Log10(sfxVolume) * 20);
    }
    public void PlayMainMenuMusic()
    {
        if (PrimaryMusicSource.clip == MainMenu)
            return;
        PrimaryMusicSource.clip = MainMenu;
        PrimaryMusicSource.Play();
    }

    void StartSound(AudioSource source, AudioClip clip, float volume)
    {
        if (source.clip == clip)
            return;
        source.clip = clip; source.Play(); source.volume = volume;
    }

    public void StartZone1Music()
    {
        StartSound(PrimaryMusicSource, AquamarinePause, 0f);
        StartSound(Phase1Source, AquamarinePhase1, 0f);
        StartSound(Phase2Source, AquamarinePhase2, 0f);
        StartSound(Phase3Source, AquamarinePhase3, 0f);
        StartSound(Phase4Source, AquamarinePhase4, 0f);
    }
    public void StartZone2Music()
    {
        StartSound(PrimaryMusicSource, EmeraldPause, 0f);
        StartSound(Phase1Source, EmeraldPhase1, 0f);
        StartSound(Phase2Source, EmeraldPhase2, 0f);
        StartSound(Phase3Source, EmeraldPhase3, 0f);
        StartSound(Phase4Source, EmeraldPhase4, 0f);
    }
    public void StartZone3Music()
    {
        StartSound(PrimaryMusicSource, HeliodorPause, 0f);
        StartSound(Phase1Source, HeliodorPhase1, 0f);
        StartSound(Phase2Source, HeliodorPhase2, 0f);
        StartSound(Phase3Source, HeliodorPhase3, 0f);
        StartSound(Phase4Source, HeliodorPhase4, 0f);
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