using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class PlayerVitals2D : MonoBehaviour, Damageable2D
{

    [Header("Health and infiction")] [SerializeField, Min(1f)]
    private float maxHealth = 100f; 

    [SerializeField] private InfectionProfile profile;
    [SerializeField, Range(0, 5)] private int infectionStage;

    [SerializeField] private float invincibilityFrames;
    
    
    [Header("Poison")]
    [SerializeField, Range(1, 8)] private int maximumPoisonLevel = 4;
    [SerializeField, Min(1f)] private float basePoisonCapacity = 100f;
    [SerializeField, Min(0f)] private float poisonCapacityScaling = 0.5f;
    
    [SerializeField, Min(0f)] private float basePoisonDamagePerSecond = 1.5f;
    [SerializeField, Range(1f, 2f)] private float poisonDamageScaling = 1.5f;
    [SerializeField, Min(0.05f)] private float poisonDamageInterval = 0.5f;
    
    
    [SerializeField, Min(0f)] private float currentPoison;

    private float poisonDamageTimer;

    

    [Header("Enviroment")] [SerializeField, Min(0.02f)]
    private float sampleInterval = 0.1f;

    [SerializeField, Min(0f)] private float fireDamagePerSecond = 8f;
    [SerializeField, Min(0f)] private float smokeDamagePerSecond = 8f;
    [SerializeField, Range(1, 32)] private int dangerousSmokeDensity = 4;
    [SerializeField, Min(0f)] private float smokeGraceSeconds = 1.5f;

    [SerializeField, Min(0.1f)] private float burnDuration = 2f;
    [SerializeField, Min(0f)] private float wetProtectionSeconds = 1.5f;

    [SerializeField, Min(1f)] private float fireDamageToCure = 30f;


    [Header("Visual Feedback")] [SerializeField]
    private SpriteRenderer characterRenderer;

    [SerializeField] private Light2D visionLight;
    [SerializeField] private Light2D burningLight;

    [Tooltip("A child sprite with flame artwork. Enabled only while the player is burning.")] [SerializeField]
    private SpriteRenderer burningVisual;

    [SerializeField] private Color damageTint = new Color(1f, 0.35f, 0.30f);
    [SerializeField, Min(0f)] private float damageFlashSeconds = 0.12f;

    [Header("Audio feedback — optional clips")] [SerializeField]
    private AudioSource damageAudio;

    [SerializeField] private AudioClip damageClip;
    [SerializeField] private AudioSource burningAudio;
    [SerializeField] private AudioClip burningLoop;



    public event Action Died;
    public event Action<float> Damaged;
    public event Action<int> InfectionChanged;
    public event Action<bool> BurningChanged;
    public InfectionProfile Profile => profile;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => Mathf.Max(1f, maxHealth * CurrentStage.healthMultiplier);
    public int InfectionStage => infectionStage;

    public InfectionProfile.InfectionStage CurrentStage => profile != null
        ? profile.GetStage(infectionStage)
        : InfectionProfile.GetDefaultStage(infectionStage);

    public bool IsDead { get; private set; }
    public bool IsBurning => !IsDead && burningSeconds > 0f;
    public bool IsWet => wetSeconds > 0f;

    public Vector2 BodyCenter =>
        bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;

    public bool IsInSmoke => smokeExposure >= smokeGraceSeconds && denseSmoke;

    public float PurificationProgress =>
        infectionStage == 0 ? 0f : Mathf.Clamp01(purificationDamage / Mathf.Max(1f, fireDamageToCure));



    public float IncomingDamageMultiplier { get; set; } = 1f;
    public bool ReducedFlashing { get; set; }


    private PlayerMotor2D motor;
    private Collider2D bodyCollider;

    private float currentHealth;
    private float invincibilitySeconds;
    
    //Poison things
    
    public int PoisonLevel => CalculatePoisonLevel();

    public float TotalPoison => currentPoison;

    public float MaximumTotalPoison =>
        GetPoisonThreshold(maximumPoisonLevel);

    public bool HasPoison =>
        currentPoison > 0.01f;

    public bool IsPoisoned =>
        PoisonLevel > 0;

    public bool IsPoisonSaturated =>
        currentPoison >= MaximumTotalPoison - 0.01f;

    public float CurrentPoison
    {
        get
        {
            int level = PoisonLevel;

            if (level >= maximumPoisonLevel)
                return MaxPoison;

            return currentPoison - GetPoisonThreshold(level);
        }
    }

    public float MaxPoison
    {
        get
        {
            int barLevel = Mathf.Min(
                PoisonLevel,
                maximumPoisonLevel - 1);

            return GetPoisonLevelCapacity(barLevel);
        }
    }
    
    
    private float GetPoisonLevelCapacity(int level)
    {
        return basePoisonCapacity *
               (1f + poisonCapacityScaling * level);
    }

    private float GetPoisonThreshold(int level)
    {
        float threshold = 0f;

        for (int i = 0; i < level; i++)
        {
            threshold += GetPoisonLevelCapacity(i);
        }

        return threshold;
    }

    private int CalculatePoisonLevel()
    {
        int level = 0;

        while (level < maximumPoisonLevel &&
               currentPoison >= GetPoisonThreshold(level + 1))
        {
            level++;
        }

        return level;
    }

    private float burningSeconds;
    private float wetSeconds;

    private float smokeExposure;
    private float purificationDamage;
    private float sampleTimer;
    private float flashSeconds;
    private float nextDamageSoundTime;

    private float baseVisionRadius;
    private float baseVisionInnerRadius;
    private float baseFireIntensity;
    private float baseBurnVolume = 1f;
    private Vector3 baseFlameScale;
    private Color originalTint = Color.white;
    private bool denseSmoke;
    private bool displayedBurning;


    private void Awake()
    {
        motor = GetComponent<PlayerMotor2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (characterRenderer == null) characterRenderer = GetComponentInChildren<SpriteRenderer>();
        if (characterRenderer != null) originalTint = characterRenderer.color;

        if (visionLight != null)
        {
            baseVisionRadius = visionLight.pointLightOuterRadius;
            baseVisionInnerRadius = visionLight.pointLightInnerRadius;
        }

        if (burningLight != null) baseFireIntensity = burningLight.intensity;
        if (burningAudio != null) baseBurnVolume = burningAudio.volume;
        if (burningVisual != null) baseFlameScale = burningVisual.transform.localScale;
        currentHealth = MaxHealth;
        ApplyStageModifiers();
        RefreshBurningFeedback();
    }

    private void Update()
    {
        float seconds = Time.deltaTime;
        invincibilitySeconds = Mathf.Max(0f, invincibilitySeconds - seconds);
        flashSeconds = Mathf.Max(0f, flashSeconds - seconds);
        if (IsDead) return;

        wetSeconds = Mathf.Max(0f, wetSeconds - seconds);
        
        

        if (IsBurning)
        {
            float burnTime = Mathf.Min(seconds, burningSeconds);
            burningSeconds = Mathf.Max(0f, burningSeconds - seconds);
            ApplyFireDamage(fireDamagePerSecond * burnTime);

        }

        UpdatePoisonDamage(seconds);
        
        


    }


    private void LateUpdate()
    {
        if (characterRenderer != null)
        {
            var stage = CurrentStage;
            

            // has its own complete animation override.
            bool hasStageArt = stage.animationOverride != null;

            Color stageTint =
                originalTint *
                (hasStageArt ? Color.white : stage.placeHolderTint);
            
            
            
            float flash = flashSeconds > 0f ? (ReducedFlashing ? 0.15f : 0.65f) : 0f;
            characterRenderer.color = Color.Lerp(stageTint, damageTint, flash);
        }

        if (!IsBurning) return;

        float pulse = ReducedFlashing ? 1f : 1f + Mathf.Sin(Time.time * 5f) * 0.06f;
        if (burningLight != null) burningLight.intensity = baseFireIntensity * pulse;
        if (burningVisual != null)
            burningVisual.transform.localScale = Vector3.Scale(baseFlameScale, new Vector3(2f - pulse, pulse, 1f));
    }


    public void TakeDamage(float damage)
    {
        if (IsDead || invincibilitySeconds > 0f || damage <= 0f) return;
        float damageTaken = ApplyDamage(damage * DamageScale);
    }

    public void TakeFireDamage(float damage)
    {
        if (!IsDead && damage > 0f) ApplyFireDamage(damage);
    }

    public void Ignite(float seconds = 2f)
    {
        if (IsDead || IsWet || seconds <= 0f) return;
        burningSeconds = Mathf.Max(burningSeconds, seconds * Mathf.Max(0.1f, CurrentStage.fireDamageMultiplier));
        RefreshBurningFeedback();
    }

    public void Extinguish()
    {
        burningSeconds = 0f;
        RefreshBurningFeedback();
    }


    public float AddPoison(float amount)
    {
        if (IsDead || amount <= 0f || IsPoisonSaturated)
            return 0f;

        float resistance =
            Mathf.Clamp01(CurrentStage.poisonResistance);

        float previousPoison = currentPoison;

        currentPoison = Mathf.Min(
            MaximumTotalPoison,
            currentPoison + amount * (1f - resistance));

        return currentPoison - previousPoison;
    }

    public float CleansePoison(float amount)
    {
        if (!HasPoison || amount <= 0f)
            return 0f;

        float previousPoison = currentPoison;
        int previousLevel = PoisonLevel;

        currentPoison = Mathf.Max(
            0f,
            currentPoison - amount);

        // Crossing below level one completely removes the remaining exposure.
        if (previousLevel > 0 && PoisonLevel == 0)
        {
            currentPoison = 0f;
        }

        return previousPoison - currentPoison;
    }
    private void UpdatePoisonDamage(float seconds)
    {
        if (IsDead || !IsPoisoned)
        {
            poisonDamageTimer = 0f;
            return;
        }

        poisonDamageTimer -= seconds;

        if (poisonDamageTimer > 0f)
            return;

        poisonDamageTimer = poisonDamageInterval;

        float levelStrength = PoisonLevel;

        if (PoisonLevel < maximumPoisonLevel)
        {
            levelStrength += CurrentPoison / MaxPoison;
        }

        float damagePerSecond =
            basePoisonDamagePerSecond *
            Mathf.Pow(levelStrength, poisonDamageScaling);

        ApplyDamage(
            damagePerSecond * poisonDamageInterval);
    }

    public void SetWet(float seconds = 1.5f)
    {
        if (IsDead) return;
        wetSeconds = Mathf.Max(wetSeconds, Mathf.Max(0f, seconds));
        Extinguish();
    }

    public void Heal(float amount)
    {
        if (!IsDead && amount > 0f) currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
    }

    //other sources of damage
    public void Kill()
    {
        if (!IsDead) ApplyDamage(currentHealth);
    }


    public void SetInfectionStage(int stage)
    {
        int nextStage = Mathf.Clamp(stage, 0, InfectionProfile.InfectionCount - 1);
        bool chanegd = nextStage != infectionStage;
        infectionStage = nextStage;
        purificationDamage = 0f;
        currentHealth = Mathf.Min(currentHealth, MaxHealth);
        ApplyStageModifiers();
        if (chanegd) InfectionChanged?.Invoke(infectionStage);
    }

    public void ResetForSpawn(int stage, float health = -1f)
    {
        
        IsDead = false;
        invincibilitySeconds = invincibilityFrames;
        wetSeconds = smokeExposure = flashSeconds = sampleTimer = 0f;
        denseSmoke = false;
        Extinguish();
        SetInfectionStage(stage);
        currentHealth = health < 0f ? MaxHealth : Mathf.Clamp(health, 1f, MaxHealth);

        currentPoison /= 2;
        poisonDamageTimer = 0f;
        
        
    }

    private float DamageScale => Mathf.Max(0f, IncomingDamageMultiplier);

    public float ApplyDamage(float amount)
    {
        if (IsDead || amount <= 0) return 0f;
        float damageTaken = Mathf.Min(currentHealth, amount);
        currentHealth -= damageTaken;
        flashSeconds = damageFlashSeconds;
        invincibilitySeconds = invincibilityFrames;

        Damaged?.Invoke(damageTaken);


        if (damageAudio != null && damageClip != null && Time.time >= nextDamageSoundTime)
        {
            damageAudio.PlayOneShot(damageClip, GameOptions.EffectsVolume);
            nextDamageSoundTime = Time.time + 0.5f;
        }

        if (currentHealth <= 0f)
        {
            IsDead = true;
            Extinguish();
            Died?.Invoke();
        }

        return damageTaken;

    }

    public void ApplyFireDamage(float baseDamage)
    {
        //use same func and scale damage with infection debuff
        float damageTaken = ApplyDamage(baseDamage * Mathf.Max(0.1f, CurrentStage.fireDamageMultiplier) * DamageScale);
        if (IsDead || infectionStage == 0 || damageTaken <= 0f) return;
        purificationDamage += damageTaken;
        float cost = Mathf.Max(1f, fireDamageToCure);

        while (purificationDamage >= cost && infectionStage > 0)
        {
            float remaining = purificationDamage - cost;
            SetInfectionStage(infectionStage - 1);
            purificationDamage = infectionStage == 0 ? 0f : remaining;

        }



    }

    private void ApplyStageModifiers()
    {

        if (visionLight != null)
        {
            float scale = Mathf.Max(0.1f, CurrentStage.visionMultiplier);
            visionLight.pointLightOuterRadius = baseVisionRadius * scale;
            visionLight.pointLightInnerRadius = baseVisionInnerRadius * scale;
        }
    }
    private void RefreshBurningFeedback()
    {
        bool burning = IsBurning;
        if (burningLight != null) burningLight.enabled = burning;
        if (burningVisual != null) burningVisual.enabled = burning;
        if (burningAudio != null)
        {
            burningAudio.volume = baseBurnVolume * GameOptions.EffectsVolume;
            if (burning && burningLoop != null && !burningAudio.isPlaying)
            {
                burningAudio.clip = burningLoop;
                burningAudio.loop = true;
                burningAudio.Play();
            }
            else if (!burning && burningAudio.isPlaying) burningAudio.Stop();
        }

        if (displayedBurning != burning)
        {
            displayedBurning = burning;
            BurningChanged?.Invoke(burning);
        }
    }

    private void OnDisable()
    {
        Extinguish();
        if (characterRenderer != null) characterRenderer.color = originalTint;
    }
}