using UnityEngine;

//emits real CellWorld fire from points attached to a moving body
public sealed class FireEmitter2D : MonoBehaviour
{
    [SerializeField] private CellWorldRoomManager roomManager;
    [SerializeField] private Transform[] points = new Transform[0];

    [Header("Fire")]
    [SerializeField, Min(0.02f)] private float emitEvery = 0.12f;
    [SerializeField, Range(1, 12)] private int puffsPerEmit = 1;
    [SerializeField, Range(1, 12)] private int firePower = 1;
    
    [SerializeField, Range(0, 4)] private int puffSize;
    [SerializeField, Range(1, 3)] private int puffLevel = 1;
    
    private bool burning;
    private float nextEmit;

    private void Awake()
    {
       
        if (roomManager == null)
            roomManager = FindFirstObjectByType<CellWorldRoomManager>();
    }

    private void Update()
    {
        if (!burning || Time.time < nextEmit) return;

        CellWorld2D world =
            roomManager != null ? roomManager.CurrentWorld : null;

        if (world == null) return;

        for (int i = 0; i < puffsPerEmit; i++)
        {
            //pick a random body point for each puff
            Transform point = points.Length > 0
                ? points[Random.Range(0, points.Length)]
                : transform;

            //randomley choose between 3 levels of intensity
            if (point != null)
            {
                //higher levels can randomly make larger puffs
                int puffSize = Random.Range(0, puffLevel);

                world.Ignite(
                    point.position,
                    puffSize,
                    (byte)firePower);
            }
        }

        //wait before making the next group of puffs
        nextEmit = Time.time + emitEvery;
    }

    public void SetBurning(bool value)
    {
        if (burning == value) return;

        burning = value;

        //emit immediately when burning begins
        if (burning)
            nextEmit = 0f;
    }
}
