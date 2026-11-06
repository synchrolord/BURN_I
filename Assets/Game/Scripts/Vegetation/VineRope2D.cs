using UnityEngine;
using System.Collections.Generic;

//move and draw one generated vine
public sealed class VineRope2D : MonoBehaviour
{
    private const float wantedPointDistance = 0.2f;
    private const int constraintPasses = 10;
    private const float spriteOverlap = 0.08f;

    private Transform startPoint;
    private Transform endPoint;

    private Vector2[] points;
    private Vector2[] oldPoints;

    private SpriteRenderer[] parts;
    private List<SpriteRenderer>[] extraParts;
    private Sprite[] bodySprites;
    public float MaxStretch { get; set; } = 2f;
    public float BurnResistance { get; set; } = 1f;
    private float[] partAngles;
    private int[] partEnds;

    private float pointDistance;
    private float damping;
    private float swayStrength;
    private float swaySpeed;
    private bool attachedEnd;
    private bool ready;

    
    private int fireSize;
    private int firePower;
    private int breakSize;
    private int breakPower;

    
    private const float fireCheckEvery = 0.1f;

    private CellWorldRoomManager roomManager;

    private bool[] burning;
    private float[] burn;
    private float[] wetUntil;

    private float burnTime;
    private float burnSpread;
    private float brokenLife;
    
    private float nextFireCheck;
    
    
    private bool[] behindFire;

    private int brokenLink = -1;
    public bool IsBroken => brokenLink >= 0;
    public void Extinguish(float protectionSeconds)
    {
        if (burning == null) return;
        for (int i = 0; i < burning.Length; i++)
        {
            burning[i] = false;
            wetUntil[i] = Mathf.Max(wetUntil[i], Time.time + protectionSeconds);
        }
    }

    public bool IsBurning
    {
        get
        {
            if (burning == null) return false;
            foreach (bool pointBurning in burning)
                if (pointBurning) return true;
            return false;
        }
    }

    //change the spacing without rebuilding sprites or losing burn progress
    public void SetLength(float length)
    {
        if (ready && !IsBroken)
            pointDistance = Mathf.Max(0.1f, length) / (points.Length - 1);
    }

    public void Setup(
        Transform start,
        Transform end,
        float ropeLength,
        Sprite[] vineSprites,
        Sprite[] endSprites,
        Sprite[] flowerSprites,
        float flowerChance,
        int sortingOrder,
        float newDamping,
        float newSwayStrength,
        float newSwaySpeed,
        CellWorldRoomManager newRoomManager,
        float newBurnTime,
        float newBurnSpread,
        float newBrokenLife,
        float newLifeRandom,
        int newFireSize,
        int newFirePower,
        int newBreakSize,
        int newBreakPower)
    {
        startPoint = start;
        endPoint = end;
        attachedEnd = endPoint != null;

        damping = Mathf.Clamp01(newDamping);
        swayStrength = newSwayStrength;
        swaySpeed = newSwaySpeed;

        CreatePoints(Mathf.Max(0.1f, ropeLength));
        
        roomManager = newRoomManager;

        burnTime = newBurnTime;
        burnSpread = newBurnSpread;
//stop several broken vines disappearing together
        brokenLife = Mathf.Max(
            0.1f,
            newBrokenLife +
            Random.Range(-newLifeRandom, newLifeRandom));

        fireSize = newFireSize;
        firePower = newFirePower;
        breakSize = newBreakSize;
        breakPower = newBreakPower;
        burning = new bool[points.Length];
        burn = new float[points.Length];
        wetUntil = new float[points.Length];
        behindFire = new bool[points.Length];
        CreateParts(
            vineSprites,
            endSprites,
            flowerSprites,
            flowerChance,
            sortingOrder);

        ready = true;
        //spread fire sampling across frames when many vines are together
        nextFireCheck = Time.time + Random.Range(0f, fireCheckEvery);
    }


    private void CreatePoints(float ropeLength)
    {
        int segmentCount = Mathf.Max(
            2,
            Mathf.CeilToInt(
                ropeLength / wantedPointDistance));

        pointDistance = ropeLength / segmentCount;

        points = new Vector2[segmentCount + 1];
        oldPoints = new Vector2[segmentCount + 1];

        Vector2 start = startPoint.position;
        Vector2 end = attachedEnd
            ? endPoint.position
            : start + Vector2.down * ropeLength;

        float directLength = Vector2.Distance(start, end);

        //start close to the final hanging shape
        float sag = attachedEnd
            ? 0.5f * Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    ropeLength * ropeLength -
                    directLength * directLength))
            : 0f;

        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);

            Vector2 wantedPoint =
                Vector2.Lerp(start, end, t) +
                Vector2.down *
                (4f * t * (1f - t) * sag);

            points[i] = wantedPoint;

            oldPoints[i] = points[i];
        }
    }


    private void CreateParts(
        Sprite[] vineSprites,
        Sprite[] endSprites,
        Sprite[] flowerSprites,
        float flowerChance,
        int sortingOrder)
    {
        bodySprites = vineSprites;
        parts = new SpriteRenderer[points.Length];
        extraParts = new List<SpriteRenderer>[points.Length];
        partAngles = new float[points.Length];
        partEnds = new int[points.Length];

        for (int i = 0; i < partEnds.Length; i++)
            partEnds[i] = -1;

        if (vineSprites != null && vineSprites.Length > 0)
        {
            int lastPoint = points.Length - 1;
            float ropeLength = pointDistance * lastPoint;
            float spriteLength = GetSpriteLength(vineSprites);

            //use enough sprites to cover the whole rope
            int bodyCount = Mathf.Clamp(
                Mathf.CeilToInt(ropeLength / spriteLength),
                1,
                lastPoint);

            for (int part = 0; part < bodyCount; part++)
            {
                int start = Mathf.RoundToInt(
                    part * lastPoint / (float)bodyCount);

                int end = Mathf.RoundToInt(
                    (part + 1) * lastPoint /
                    (float)bodyCount);

                Sprite sprite = RandomSprite(vineSprites);
                if (sprite == null) continue;

                SpriteRenderer partSprite = CreateSprite(
                    "Vine Part",
                    sprite,
                    start,
                    sortingOrder);

                partEnds[start] = end;

                AddFlower(
                    partSprite.transform.parent,
                    flowerSprites,
                    flowerChance,
                    sortingOrder);
            }
        }

        //only dangling vines have a loose end
        if (!attachedEnd &&
            endSprites != null &&
            endSprites.Length > 0)
        {
            int end = points.Length - 1;
            Sprite sprite = RandomSprite(endSprites);

            if (sprite != null)
            {
                CreateSprite(
                    "Vine End",
                    sprite,
                    end,
                    sortingOrder);
            }
        }
    }


    private SpriteRenderer CreateSprite(
        string objectName,
        Sprite sprite,
        int point,
        int sortingOrder)
    {
        //the parent moves while only the sprite child stretches
        GameObject partObject = new GameObject(objectName);
        partObject.transform.SetParent(transform, false);

        GameObject imageObject = new GameObject("Sprite");
        imageObject.transform.SetParent(
            partObject.transform,
            false);

        SpriteRenderer spriteRenderer =
            imageObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = sortingOrder;

        //keep sprites centered even when their imported pivot is not
        imageObject.transform.localPosition =
            -sprite.bounds.center;

        //the sprites are drawn vertically so turn them along the vine
        partAngles[point] =
            Random.value < 0.5f ? 90f : -90f;

        parts[point] = spriteRenderer;
        return spriteRenderer;
    }


    private void AddFlower(
        Transform vinePart,
        Sprite[] flowers,
        float chance,
        int sortingOrder)
    {
        if (flowers == null ||
            flowers.Length == 0 ||
            Random.value > chance)
        {
            return;
        }

        Sprite sprite = RandomSprite(flowers);
        if (sprite == null) return;

        GameObject flowerObject = new GameObject("Flower");
        flowerObject.transform.SetParent(vinePart, false);

        SpriteRenderer flower =
            flowerObject.AddComponent<SpriteRenderer>();

        flower.sprite = sprite;
        flower.sortingOrder = sortingOrder;

        flowerObject.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                Random.value < 0.5f ? 0f : 180f);
    }


    private Sprite RandomSprite(Sprite[] sprites)
    {
        return sprites[Random.Range(0, sprites.Length)];
    }


    private float GetSpriteLength(Sprite[] sprites)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return Mathf.Max(
                    0.01f,
                    sprites[i].bounds.size.y);
        }

        return 1f;
    }


    private void OnEnable()
    {
        if (!ready) return;

        //forget old movement after being disabled
        for (int i = 0; i < points.Length; i++)
            oldPoints[i] = points[i];
    }


    private void FixedUpdate()
    {
        if (!ready || startPoint == null)
            return;

        UpdateBurning();
        MovePoints();

        //restore the distance between every rope point
        for (int i = 0; i < constraintPasses; i++)
            ApplyConstraints();
    }


    private void MovePoints()
    {
        float step = Time.fixedDeltaTime;

        //skip points attached to a placed anchor
        for (int i = 1; i < points.Length; i++)
        {
            if (attachedEnd && i == points.Length - 1)
                continue;

            Vector2 current = points[i];
            Vector2 velocity =
                (current - oldPoints[i]) *
                (1f - damping);

            oldPoints[i] = current;

            float sway = Mathf.Sin(
                Time.time * swaySpeed +
                i * 0.7f) * swayStrength;

            Vector2 force =
                Physics2D.gravity +
                Vector2.right * sway;

            points[i] +=
                velocity +
                force * (step * step);
        }
    }


    private void ApplyConstraints()
    {
        points[0] = startPoint.position;

        if (attachedEnd)
            points[points.Length - 1] = endPoint.position;

        for (int i = 0; i < points.Length - 1; i++)
        {
            if (i == brokenLink) continue;
            int next = i + 1;
            Vector2 difference = points[next] - points[i];
            float distance = difference.magnitude;

            if (distance <= 0.0001f)
                continue;

            Vector2 correction =
                difference *
                ((distance - pointDistance) / distance);

            bool firstPinned = i == 0;
            bool secondPinned =
                attachedEnd &&
                next == points.Length - 1;

            if (firstPinned)
            {
                points[next] -= correction;
            }
            else if (secondPinned)
            {
                points[i] += correction;
            }
            else
            {
                points[i] += correction * 0.5f;
                points[next] -= correction * 0.5f;
            }
        }

        points[0] = startPoint.position;

        if (attachedEnd)
            points[points.Length - 1] = endPoint.position;
    }


    private void LateUpdate()
    {
        if (ready)
            UpdateParts();
    }


    private void UpdateParts()
    {
        if (parts == null)
            return;

        for (int i = 0; i < parts.Length; i++)
        {
            SpriteRenderer partSprite = parts[i];
            if (partSprite == null) continue;

            Transform part = partSprite.transform.parent;
            int end = partEnds[i];
            
            float partBurn = GetPartBurn(i, end);
            if (partBurn > 0f) PutPartBehindFire(i, part);
            partSprite.color = BurnColor(partBurn);

//hide the sprite that would visually bridge the broken rope
            if (end >= 0 &&
                brokenLink >= i &&
                brokenLink < end)
            {
                part.gameObject.SetActive(false);
                continue;
            }

            //an end sprite follows the final rope point without stretching
            if (end < 0)
            {
                Vector2 direction = i > 0
                    ? points[i] - points[i - 1]
                    : Vector2.down;

                float angle =
                    Mathf.Atan2(direction.y, direction.x) *
                    Mathf.Rad2Deg - 90f;

                part.position = points[i];
                part.rotation =
                    Quaternion.Euler(0f, 0f, angle);

                continue;
            }

            Vector2 bodyDirection =
                points[end] - points[i];

            float bodyAngle =
                Mathf.Atan2(
                    bodyDirection.y,
                    bodyDirection.x) *
                Mathf.Rad2Deg +
                partAngles[i];

            //place this sprite across its exact rope section
            part.position =
                (points[i] + points[end]) * 0.5f;

            part.rotation =
                Quaternion.Euler(0f, 0f, bodyAngle);

            float spriteLength = Mathf.Max(
                0.01f,
                partSprite.sprite.bounds.size.y);

            DrawSection(i, partSprite, bodyDirection.magnitude, spriteLength);
        }
    }

    private void DrawSection(
        int index,
        SpriteRenderer original,
        float length,
        float spriteLength)
    {
        //repeat long sections instead of stretching one sprite across them
        int count = Mathf.Max(1, Mathf.CeilToInt(
            length / (spriteLength * Mathf.Max(1f, MaxStretch))));

        List<SpriteRenderer> extras = extraParts[index];
        if (count > 1 && extras == null)
        {
            extras = new List<SpriteRenderer>();
            extraParts[index] = extras;
        }

        //keep extra sprites for later so moving vines do not rebuild every frame
        while (extras != null && extras.Count < count - 1)
        {
            GameObject image = new GameObject("Vine Repeat");
            image.transform.SetParent(original.transform.parent, false);
            SpriteRenderer repeat = image.AddComponent<SpriteRenderer>();
            repeat.sprite = RandomSprite(bodySprites);
            if (repeat.sprite == null) repeat.sprite = original.sprite;
            //choose once so the pattern does not change every frame
            repeat.flipX = Random.value < 0.5f;
            repeat.flipY = Random.value < 0.5f;
            extras.Add(repeat);
        }

        float pieceLength = length / count;
        int total = 1 + (extras != null ? extras.Count : 0);

        for (int i = 0; i < total; i++)
        {
            SpriteRenderer image = i == 0 ? original : extras[i - 1];
            image.enabled = i < count;
            if (!image.enabled) continue;

            if (i > 0)
            {
                image.sharedMaterial = original.sharedMaterial;
                image.color = original.color;
                image.sortingLayerID = original.sortingLayerID;
                image.sortingOrder = original.sortingOrder;
            }

            float stretch = (pieceLength + spriteOverlap) /
                Mathf.Max(0.01f, image.sprite.bounds.size.y);
            Vector2 center = image.sprite.bounds.center;
            if (image.flipX) center.x = -center.x;
            if (image.flipY) center.y = -center.y;
            image.transform.localScale = new Vector3(1f, stretch, 1f);
            image.transform.localPosition = new Vector3(
                -center.x,
                -length * 0.5f + pieceLength * (i + 0.5f) - center.y * stretch,
                0f);
        }
    }
    private void UpdateBurning()
    {
        bool checkFire = Time.time >= nextFireCheck;

        CellWorld2D world =
            roomManager != null
                ? roomManager.CurrentWorld
                : null;

        if (checkFire && world != null)
        {
            nextFireCheck = Time.time + fireCheckEvery;

            //water wins when fire and water touch the same rope point
            for (int i = 0; i < points.Length; i++)
            {
                if (world.GetWaterFraction(points[i]) > 0f)
                {
                    burning[i] = false;
                    wetUntil[i] = Time.time + 0.5f;
                }
                else if (Time.time >= wetUntil[i] &&
                         world.GetFireEnergy(points[i]) > 0)
                    burning[i] = true;
            }
        }

        int emittedFlames = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (!burning[i])
                continue;

            burn[i] = Mathf.MoveTowards(
                burn[i],
                1f,
                Time.fixedDeltaTime / (burnTime * Mathf.Max(1f, BurnResistance)));

            //keep a small real flame on the burning vine
            //limit new fire per rope and stop emitting from fully consumed points
            if (checkFire && world != null && i % 2 == 0 &&
                burn[i] < 1f && emittedFlames < 3)
            {
                emittedFlames++;
                world.Ignite(
                    points[i],
                    fireSize,
                    (byte)firePower);
            }
              
            //spread burning into the next rope points
            if (burn[i] >= burnSpread)
            {
                if (i > 0 && Time.time >= wetUntil[i - 1])
                    burning[i - 1] = true;

                if (i < points.Length - 1 && Time.time >= wetUntil[i + 1])
                    burning[i + 1] = true;
            }

            if (burn[i] >= 1f && brokenLink < 0)
                BreakRope(i, world);
        }
    }


    private void BreakRope(
        int point,
        CellWorld2D world)
    {
        //remove the rope link immediately below this point
        brokenLink = Mathf.Clamp(
            point,
            0,
            points.Length - 2);

        //make a stronger final puff where it breaks
        if (world != null)
        {
            world.Ignite(
                points[point],
                breakSize,
                (byte)breakPower);
        }
          
        //give the broken pieces time to fall before removing them
        Destroy(gameObject, brokenLife);
    }
    private float GetPartBurn(
        int start,
        int end)
    {
        if (end < 0)
            return burn[start];

        float hottest = 0f;

        for (int i = start; i <= end; i++)
            hottest = Mathf.Max(hottest, burn[i]);

        return hottest;
    }


    private Color BurnColor(float amount)
    {
        Color fireColor =
            new Color(1f, 0.35f, 0.05f, 1f);

        Color ashColor =
            new Color(0.12f, 0.06f, 0.03f, 1f);

        if (amount < 0.5f)
        {
            return Color.Lerp(
                Color.white,
                fireColor,
                amount * 2f);
        }

        return Color.Lerp(
            fireColor,
            ashColor,
            (amount - 0.5f) * 2f);
    }
    
    private void PutPartBehindFire(
        int partIndex,
        Transform part)
    {
        if (behindFire[partIndex] ||
            roomManager == null ||
            roomManager.CurrentWorld == null)
        {
            return;
        }

        SpriteRenderer worldImage =
            roomManager.CurrentWorld.GetComponent<SpriteRenderer>();

        if (worldImage == null)
            return;

        SpriteRenderer[] images =
            part.GetComponentsInChildren<SpriteRenderer>();

        //move this vine piece just behind the CellWorld fire
        foreach (SpriteRenderer image in images)
        {
            image.sortingLayerID =
                worldImage.sortingLayerID;

            image.sortingOrder =
                worldImage.sortingOrder - 1;
        }

        behindFire[partIndex] = true;
    }
    
}
