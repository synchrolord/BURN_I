using UnityEngine;

//use the cellworld's active room, so we don't need another set of triggers
[DefaultExecutionOrder(-50)]
public sealed class RoomAudio2D : MonoBehaviour
{
    [SerializeField] private CellWorld2D world; 
    [SerializeField] private EnemyAudio2D[] enemies = new EnemyAudio2D[0];
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(10f, 22000f)] private float cutoff = 22000f;
    [SerializeField, Range(0f, 1f)] private float distortion;
    [SerializeField] private AudioReverbPreset reverb = AudioReverbPreset.Off;

    private CellWorldRoomManager manager;
    public static RoomAudio2D Current { get; private set; }
    public float Cutoff => cutoff;
    public float Distortion => distortion;
    public AudioReverbPreset Reverb => reverb;

    private void Awake()
    {
        if (world == null) world = GetComponentInChildren<CellWorld2D>(true);
        manager = FindAnyObjectByType<CellWorldRoomManager>();
    }

    private void Update()
    {
        if (manager != null && world != null && manager.CurrentWorld == world)
            Current = this;
        else if (Current == this)
            Current = null;
    }

    public float GetVolume(EnemyAudio2D enemy, float distanceVolume)
    {
        //listed enemies use the room volume, even when they are in another room
        if (enemies.Length == 0) return distanceVolume * volume;
        foreach (EnemyAudio2D sound in enemies)
            if (sound == enemy) return volume;
        return 0f;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }
}
