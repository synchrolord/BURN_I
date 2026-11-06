using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

//create hanging vines between a group of points
public sealed class vineController : MonoBehaviour
{
    private readonly List<VineRope2D> createdVines = new List<VineRope2D>();

    public bool IsBurning
    {
        get
        {
            foreach (VineRope2D rope in createdVines)
                if (rope != null && rope.IsBurning) return true;
            return false;
        }
    }

    [Header("Connections")]
    private Transform[] points;

    [SerializeField, Range(0f, 1f)]
    private float connectionChance = 0.7f;

    [SerializeField, Min(1)]
    private int maxConnectionsPerPoint = 3;

    [FormerlySerializedAs("maximumLength")]
    [SerializeField, Min(0.1f)]
    private float connectionDistance = 6f;

    //extra length controls how much the connected vines sag
    [SerializeField, Min(0f)]
    private float minLooseLength = 0.25f;

    [SerializeField, Min(0f)]
    private float maxLooseLength = 2f;

    [Header("Movement")]
    [SerializeField, Range(0f, 1f)]
    private float damping = 0.1f;

    [SerializeField, Min(0f)]
    private float swayStrength = 1f;

    [SerializeField, Min(0f)]
    private float swaySpeed = 1.5f;

    [Header("Dangling Vines")]
    [SerializeField, Range(0f, 1f)]
    private float danglingChance = 0.25f;

    [SerializeField] private Vector2 danglingLengthRange =
        new Vector2(1f, 4f);

    [Header("Sprites")]
    [SerializeField, Range(1f, 10f)] private float maxStretch = 2f;
    [SerializeField] private Object vineSheet;
    [SerializeField] private Object endSheet;
    [SerializeField] private Object flowerSheet;

    [SerializeField, HideInInspector] private Sprite[] vineSprites;
    [SerializeField, HideInInspector] private Sprite[] endSprites;
    [SerializeField, HideInInspector] private Sprite[] flowerSprites;

    [SerializeField, Range(0f, 1f)]
    private float flowerChance = 0.15f;

    [Header("Draw Order")]
    [SerializeField] private int behindOrder = 4;

    [FormerlySerializedAs("sortingOrder")]
    [SerializeField] private int frontOrder = 6;

    //same seed creates the same vines every time
    [SerializeField] private int seed = 1234;
    
    
    [Header("Burning")]
    [SerializeField] private CellWorldRoomManager roomManager;
    [SerializeField, Min(0.1f)] private float burnTime = 1.5f;
    [SerializeField, Range(0f, 1f)] private float burnSpread = 0.35f;
    [SerializeField, Min(0.1f)] private float brokenLife = 4f;
    [SerializeField, Min(0f)] private float lifeRandom = 0.75f;
    [SerializeField, Range(0, 3)] private int fireSize = 1;
    [SerializeField, Range(1, 3)] private int firePower = 2;
    [SerializeField, Range(0, 4)] private int breakSize = 2;
    [SerializeField, Range(1, 3)] private int breakPower = 3;

    private void Start()
    {
        if (roomManager == null) roomManager = FindFirstObjectByType<CellWorldRoomManager>();
        points = new Transform[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
            points[i] = transform.GetChild(i);

        BuildVines();
    }

    private void BuildVines()
    {
        if (points == null || points.Length == 0)
            return;

        int[] connections = new int[points.Length];

        //do not change randomness used by other game systems
        Random.State oldRandom = Random.state;
        Random.InitState(seed);

        ConnectPoints(connections);
        AddDanglingVines();

        Random.state = oldRandom;
    }


    private void ConnectPoints(int[] connections)
    {
        //check every pair once
        for (int first = 0; first < points.Length; first++)
        for (int second = first + 1; second < points.Length; second++)
        {
            if (points[first] == null || points[second] == null)
                continue;

            if (connections[first] >= maxConnectionsPerPoint ||
                connections[second] >= maxConnectionsPerPoint)
            {
                continue;
            }

            float directLength = Vector2.Distance(
                points[first].position,
                points[second].position);

            if (directLength <= 0.01f ||
                directLength > connectionDistance ||
                Random.value > connectionChance)
            {
                continue;
            }

            float extraLength = Random.Range(
                Mathf.Min(minLooseLength, maxLooseLength),
                Mathf.Max(minLooseLength, maxLooseLength));

            //always begin with enough length to reach both points
            float ropeLength = directLength + extraLength;

            CreateVine(
                points[first],
                points[second],
                ropeLength);

            connections[first]++;
            connections[second]++;
        }
    }


    private void AddDanglingVines()
    {
        foreach (Transform point in points)
        {
            if (point == null || Random.value > danglingChance)
                continue;

            CreateVine(
                point,
                null,
                RandomRange(danglingLengthRange));
        }
    }


    public VineRope2D CreateVine(
        Transform start,
        Transform end,
        float ropeLength)
    {
        if (roomManager == null)
            roomManager = FindAnyObjectByType<CellWorldRoomManager>();

        GameObject vineObject = new GameObject("Vine");
        vineObject.transform.SetParent(transform, false);

        VineRope2D rope =
            vineObject.AddComponent<VineRope2D>();
        rope.MaxStretch = maxStretch;

        rope.Setup(
            start,
            end,
            ropeLength,
            vineSprites,
            endSprites,
            flowerSprites,
            flowerChance,
            RandomOrder(),
            damping,
            swayStrength,
            swaySpeed,
            roomManager,
            burnTime,
            burnSpread,
            brokenLife,
            lifeRandom,
            fireSize,
            firePower,
            breakSize,
            breakPower);
        for (int i = createdVines.Count - 1; i >= 0; i--)
            if (createdVines[i] == null) createdVines.RemoveAt(i);
        createdVines.Add(rope);
        return rope;
    }


    private float RandomRange(Vector2 range)
    {
        return Random.Range(
            Mathf.Min(range.x, range.y),
            Mathf.Max(range.x, range.y));
    }


    private int RandomOrder()
    {
        //choose whether the vine is behind or in front
        return Random.value < 0.5f
            ? behindOrder
            : frontOrder;
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        LoadSprites(vineSheet, ref vineSprites);
        LoadSprites(endSheet, ref endSprites);
        LoadSprites(flowerSheet, ref flowerSprites);
    }


    //collect every sliced sprite from one imported sheet
    private void LoadSprites(
        Object sheet,
        ref Sprite[] sprites)
    {
        if (sheet == null)
            return;

        string path =
            UnityEditor.AssetDatabase.GetAssetPath(sheet);

        Object[] assets =
            UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);

        List<Sprite> found = new List<Sprite>();
        Sprite single = null;

        foreach (Object asset in assets)
        {
            if (!(asset is Sprite sprite)) continue;

            //prefer slices instead of the full aseprite image
            if (sprite.name == sheet.name)
                single = sprite;
            else
                found.Add(sprite);
        }

        //also allow a sheet containing one normal sprite
        if (found.Count == 0 && single != null)
            found.Add(single);

        sprites = found.ToArray();
    }
#endif
}
