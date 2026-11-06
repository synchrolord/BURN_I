using System.Collections;
using UnityEngine;

//build a tangled body from the same ropes used by the grapples
public sealed class BossBody2D : MonoBehaviour
{
    [SerializeField] private vineController vines;

    [SerializeField, Min(0.1f)] private float size = 1.5f;
    [SerializeField, Range(1, 48)] private int density = 16;
    [SerializeField] private int seed = 123;
    [SerializeField, Min(0.1f)] private float regenTime = 6f;
    [SerializeField, Min(1f)] private float fireResistance = 4f;
    private float fireProtectedUntil;
    private VineRope2D[] ropes;
    private Transform[] firstPoints;
    private Transform[] secondPoints;
    private float[] lengths;
    private float[] regrowAt;
    private Transform bodyPoints;

    public bool IsBurning
    {
        get
        {
            if (ropes == null) return false;
            foreach (VineRope2D rope in ropes)
                if (rope != null && rope.IsBurning) return true;
            return false;
        }
    }

    public void Extinguish(float protectionSeconds)
    {
        fireProtectedUntil = Time.time + protectionSeconds;
        if (ropes == null) return;
        foreach (VineRope2D rope in ropes)
            if (rope != null) rope.Extinguish(protectionSeconds);
    }

    private IEnumerator Start()
    {
        //let the controller finish its ordinary startup before adding ropes
        yield return null;

        if (vines == null)
        {
            Debug.LogWarning("Boss Body needs a vine controller.", this);
            yield break;
        }

        Random.State oldRandom = Random.state;
        Random.InitState(seed);

        Transform points = new GameObject("Body Points").transform;
        points.SetParent(transform, false);
        bodyPoints = points;

        ropes = new VineRope2D[density];
        firstPoints = new Transform[density];
        secondPoints = new Transform[density];
        lengths = new float[density];
        regrowAt = new float[density];
        for (int i = 0; i < density; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float opposite = angle + Mathf.PI + Random.Range(-0.7f, 0.7f);

            Transform first = MakePoint(points, angle);
            Transform second = MakePoint(points, opposite);

            //cross the middle with enough slack to make a loose bundle
            firstPoints[i] = first;
            secondPoints[i] = second;
            lengths[i] = Vector2.Distance(first.position, second.position) + size * 0.25f;
            GrowVine(i);
        }

        Random.state = oldRandom;
    }

    private void Update()
    {
        if (ropes == null || vines == null) return;

        for (int i = 0; i < ropes.Length; i++)
        {
            if (ropes[i] != null && !ropes[i].IsBroken) continue;

            if (regrowAt[i] == 0f)
            {
                //spread recovery out when the whole bundle burns together
                regrowAt[i] = Time.time + regenTime + Random.Range(0f, 1f);
            }

            //let the old burning pieces disappear before replacing this vine
            if (ropes[i] == null && Time.time >= regrowAt[i])
            {
                GrowVine(i);
                regrowAt[i] = 0f;
                return; //only build one replacement per frame
            }
        }
    }

    private void GrowVine(int index)
    {
        ropes[index] = vines.CreateVine(
            firstPoints[index], secondPoints[index], lengths[index]);
        ropes[index].transform.SetParent(bodyPoints, true);
        ropes[index].BurnResistance = fireResistance;
        //new growth shares the body's current fire protection
        if (Time.time < fireProtectedUntil)
            ropes[index].Extinguish(fireProtectedUntil - Time.time);
    }

    private Transform MakePoint(Transform parent, float angle)
    {
        Transform point = new GameObject("Body Point").transform;
        point.SetParent(parent, false);

        float radius = size * 0.5f * Random.Range(0.55f, 1f);
        point.localPosition = new Vector3(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius,
            0f);

        return point;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(Vector3.zero, size * 0.5f);
    }
}
