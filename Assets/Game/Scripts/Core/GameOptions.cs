using UnityEngine;

/// <summary>Small persistent player preferences. No save-game or scene state lives here.</summary>
public static class GameOptions
{
    public enum Difficulty { Story, Standard, Demanding }
    private const string Prefix = "BURN.Options.";
    public static bool ReducedFlashing { get; private set; } = PlayerPrefs.GetInt(Prefix + "ReducedFlashing", 1) == 1;
    public static Difficulty Challenge { get; private set; } = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Difficulty", 1), 0, 2);
    public static float MusicVolume { get; private set; } = PlayerPrefs.GetFloat(Prefix + "Music", 0.45f);
    public static float EffectsVolume { get; private set; } = PlayerPrefs.GetFloat(Prefix + "Effects", 0.7f);
    public static float DamageMultiplier => Challenge == Difficulty.Story ? 0.65f : Challenge == Difficulty.Demanding ? 1.3f : 1f;

    public static void SetReducedFlashing(bool value) { ReducedFlashing = value; PlayerPrefs.SetInt(Prefix + "ReducedFlashing", value ? 1 : 0); }
    public static void SetDifficulty(Difficulty value) { Challenge = value; PlayerPrefs.SetInt(Prefix + "Difficulty", (int)value); }
    public static void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume); }
    public static void SetEffectsVolume(float value) { EffectsVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "Effects", EffectsVolume); }
    public static void Save() => PlayerPrefs.Save();
}
