using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DialoguePopup : MonoBehaviour
{
    private static DialoguePopup current;
    private DialogueTrigger2D.Entry[] entries;
    private DialoguePortraits portraits;
    private PlayerVitals2D player;
    private CameraFollow2D cameraFollow;
    private readonly List<Behaviour> controls = new List<Behaviour>();
    private RectTransform panel;
    private RectTransform portrait;
    private Image bodyImage;
    private Image boxImage;
    private GameObject boxAnimation;
    private SpriteRenderer[] boxRenderers = new SpriteRenderer[0];
    private Image[] boxLayers = new Image[0];
    private int boxStage = -1;
    private Image faceImage;
    private Animator faceAnimator;
    private SpriteRenderer faceRenderer;
    private bool hasFaceState;
    private TMP_Text text;
    private TMP_Text placeholder;
    private Rigidbody2D playerBody;
    private Vector2 oldVelocity;
    private float oldAngularVelocity;
    private float oldTimeScale;
    private bool oldAudioPause;
    private bool ownsPause;
    private bool ready;
    private bool closing;
    private int index;

    public static bool Show(DialogueTrigger2D.Entry[] lines, DialoguePortraits art, PlayerVitals2D target)
    {
        if (current != null || Time.timeScale == 0f) return false;
        GameObject root = new GameObject("Dialogue Popup");
        current = root.AddComponent<DialoguePopup>();
        current.Begin(lines, art, target);
        return true;
    }

    private void Begin(DialogueTrigger2D.Entry[] lines, DialoguePortraits art, PlayerVitals2D target)
    {
        entries = lines;
        portraits = art;
        player = target;
        playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null) { oldVelocity = playerBody.linearVelocity; oldAngularVelocity = playerBody.angularVelocity; }
        oldTimeScale = Time.timeScale;
        oldAudioPause = AudioListener.pause;
        ownsPause = true;
        //remember only enabled controls, so closing doesn't turn on anything previously disabled
        foreach (Behaviour control in player.GetComponentsInChildren<Behaviour>(true))
            if (control.enabled && (control is PlayerInput || control is PlayerMotor2D ||
                control is PressureTool2D || control is GrenadeThrower2D || control is PlayerAim2D ||
                control is PlayerLiquidInteraction2D))
            { controls.Add(control); control.enabled = false; }
        Time.timeScale = 0f;
        AudioListener.pause = true;
        cameraFollow = FindAnyObjectByType<CameraFollow2D>();
        if (cameraFollow != null) cameraFollow.SetDialogueZoom(true);
        BuildUI();
        BuildFace();
        SetEntry();
        StartCoroutine(Enter());
    }

    private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TMP_Text Label(RectTransform rect, int size)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (portraits != null && portraits.font != null) label.font = portraits.font;
        label.fontSize = size;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640, 360);
        scaler.matchWidthOrHeight = 0f;
        panel = Rect("Box", transform, new Vector2(0, -170), new Vector2(600, 150));
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        Image box = panel.gameObject.AddComponent<Image>();
        boxImage = box;
        box.sprite = portraits != null ? portraits.box : null;
        box.color = box.sprite != null ? Color.white : new Color(0.03f, 0.03f, 0.03f, 0.96f);
        portrait = Rect("Portrait", panel, new Vector2(16, 22), new Vector2(112, 112));
        bodyImage = portrait.gameObject.AddComponent<Image>();
        faceImage = Rect("Face", portrait, Vector2.zero, new Vector2(112, 112)).gameObject.AddComponent<Image>();
        placeholder = Label(Rect("Placeholder", portrait, new Vector2(4, 4), new Vector2(104, 104)), 14);
        text = Label(Rect("Text", panel, new Vector2(144, 24), new Vector2(440, 110)), 18);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = 18;
        text.wordSpacing = portraits != null ? portraits.wordSpacing : 5f;
        text.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text hint = Label(Rect("Continue", panel, new Vector2(144, 4), new Vector2(440, 18)), 12);
        hint.text = "Click to continue";
    }

    private void BuildFace()
    {
        if (portraits == null || portraits.faces == null) return;
        GameObject face = Instantiate(portraits.faces, transform);
        faceAnimator = face.GetComponentInChildren<Animator>();
        faceRenderer = face.GetComponentInChildren<SpriteRenderer>();

        foreach (Renderer renderer in face.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = true;
        if (faceAnimator != null)
        {
            faceAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            faceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private void UpdateBox()
    {
        if (player == null) return;
        int stage = player.InfectionStage;
        if (stage != boxStage)
        {
            boxStage = stage;
            if (boxAnimation != null) Destroy(boxAnimation);
            foreach (Image layer in boxLayers) if (layer != null) Destroy(layer.gameObject);
            boxLayers = new Image[0];
            boxRenderers = new SpriteRenderer[0];
            GameObject prefab = portraits != null && portraits.boxes != null && stage < portraits.boxes.Length
                ? portraits.boxes[stage] : null;
            if (prefab != null)
            {
                boxAnimation = Instantiate(prefab, transform);
                foreach (Animator animator in boxAnimation.GetComponentsInChildren<Animator>())
                {
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                boxRenderers = boxAnimation.GetComponentsInChildren<SpriteRenderer>();
                System.Array.Sort(boxRenderers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
                boxLayers = new Image[boxRenderers.Length];
                for (int i = 0; i < boxRenderers.Length; i++)
                {
                    boxRenderers[i].forceRenderingOff = true;
                    Image layer = Rect("Box Layer", panel, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
                    layer.raycastTarget = false;
                    //lowest layer is the background; extra layers can drape over the portrait
                    if (i == 0) layer.transform.SetAsFirstSibling();
                    boxLayers[i] = layer;
                }
            }
        }
        bool hasBackground = false;
        for (int i = 0; i < boxLayers.Length; i++)
        {
            Sprite sprite = boxRenderers[i].sprite;
            Image layer = boxLayers[i];
            layer.enabled = sprite != null && boxRenderers[i].enabled;
            if (!layer.enabled) continue;
            if (i == 0) hasBackground = true;
            layer.sprite = sprite;
            layer.color = boxRenderers[i].color;
            Vector2 scale = new Vector2(600f / 320f, 150f / 80f);
            layer.rectTransform.sizeDelta = Vector2.Scale(sprite.rect.size, scale);
            layer.rectTransform.anchoredPosition = new Vector2(300f, 75f) - Vector2.Scale(sprite.pivot, scale);
        }
        boxImage.enabled = !hasBackground;
    }
    private void SetEntry()
    {
        text.text = entries[index] != null ? entries[index].text : "";
        hasFaceState = false;
        if (faceAnimator == null) return;
        string emotion = (entries[index] != null ? entries[index].emotion : DialoguePortraits.Emotion.Neutral).ToString();
        int state = Animator.StringToHash(emotion);
        if (!faceAnimator.HasState(0, state)) state = Animator.StringToHash("Neutral");
        hasFaceState = faceAnimator.HasState(0, state);
        if (hasFaceState) faceAnimator.Play(state, 0, 0f);
    }

    private void LateUpdate()
    {
        if (faceImage == null) return;
        UpdateBox();
        Sprite sprite = faceRenderer != null ? faceRenderer.sprite : null;
        faceImage.enabled = hasFaceState && sprite != null && player != null &&
            player.InfectionStage != InfectionProfile.InfectionCount - 1;
        if (!faceImage.enabled) return;
        faceImage.sprite = sprite;
        
        float scale = 112f / 32f;
        faceImage.rectTransform.sizeDelta = sprite.rect.size * scale;
        faceImage.rectTransform.anchoredPosition = new Vector2(56f, 56f) - sprite.pivot * scale;
    }

    private IEnumerator Enter()
    {
        yield return Slide(-170f, 20f);

        while (Mouse.current != null && Mouse.current.leftButton.isPressed) yield return null;
        ready = true;
    }

    private void Update()
    {
        if (player == null) { Destroy(gameObject); return; }
        int stage = player.InfectionStage;
        bool consumed = stage == InfectionProfile.InfectionCount - 1;
        DialoguePortraits.Emotion emotion = entries[index] != null ? entries[index].emotion : DialoguePortraits.Emotion.Neutral;
        Sprite body = portraits != null && portraits.stages != null && stage < portraits.stages.Length ? portraits.stages[stage] : null;
        bodyImage.sprite = body;
        bodyImage.color = body != null ? Color.white : player.CurrentStage.placeHolderTint;


        placeholder.enabled = body == null;
        placeholder.text = player.CurrentStage.name + "\n" + (consumed ? "..." : emotion.ToString());
        portrait.anchoredPosition = new Vector2(16f, 22f + Mathf.Sin(Time.unscaledTime * (consumed ? 1f : 3f)) * 2f);
        if (!ready || closing) return;
        if ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            if (index + 1 < entries.Length) { index++; SetEntry(); }
            else { closing = true; StartCoroutine(Close()); }
        }
    }

    private IEnumerator Slide(float from, float to)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f)
        {
            panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        panel.anchoredPosition = new Vector2(0f, to);
    }

    private IEnumerator Close()
    {
        if (cameraFollow != null) cameraFollow.SetDialogueZoom(false);
        yield return Slide(20f, -170f);
        yield return new WaitForSecondsRealtime(0.3f);
        while ((Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)) yield return null;
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (!ownsPause) return;
        ownsPause = false;
        if (cameraFollow != null) cameraFollow.SetDialogueZoom(false);
        Time.timeScale = oldTimeScale;
        AudioListener.pause = oldAudioPause;
        foreach (Behaviour control in controls)
            if (control != null) control.enabled = true;
        if (playerBody != null) { playerBody.linearVelocity = oldVelocity; playerBody.angularVelocity = oldAngularVelocity; }
        if (current == this) current = null;
    }
}
