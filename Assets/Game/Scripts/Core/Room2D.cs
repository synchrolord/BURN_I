using UnityEngine;

//one shared simulation for all the screens and tilemaps in this room
[DefaultExecutionOrder(-200)]
public sealed class Room2D : MonoBehaviour
{
    [Tooltip("Room width and height in world units. The room's position is its bottom-left corner.")]
    [SerializeField] private Vector2 size = new Vector2(80f, 22.5f);
    [Tooltip("Smaller cells give more detail and cost more. Resize the room here instead of scaling its Transform.")]
    [SerializeField, Min(0.0625f)] private float cellSize = 0.25f;
    [SerializeField] private CellWorld2D world;
    public Vector2 Size => size;
    public CellWorld2D World => world;

    public void Configure(CellWorld2D source) { world = source; ApplySize(); }

    public bool ApplySize()
    {
        if (world == null) return false;
        int columns = Mathf.CeilToInt(size.x / cellSize);
        int rows = Mathf.CeilToInt(size.y / cellSize);
        if (columns < 16 || rows < 16 || columns > 2048 || rows > 2048 || (long)columns * rows > 1048576)
        {
            Debug.LogError("Room size needs 16–2048 cells per side and at most 1,048,576 cells. Increase Cell Size for a larger room.", this);
            return false;
        }
        //the room's origin is its bottom-left; moving it carries every child with it
        world.transform.localPosition = Vector3.zero;
        world.ConfigureGrid(columns, rows, cellSize);
        return true;
    }

    private void Awake() => ApplySize();

    private void Start()
    {
        CellWorldRoomManager manager = FindAnyObjectByType<CellWorldRoomManager>();
        if (manager != null) manager.RegisterWorld(world);
        CameraFollow2D camera = FindAnyObjectByType<CameraFollow2D>();
        if (camera != null) camera.UseRooms(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(size * 0.5f, size);
    }
}
