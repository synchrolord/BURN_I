using UnityEngine;

[RequireComponent(typeof(PlayerVitals2D))]
public sealed class PlayerAppearance2D : MonoBehaviour
{
    [SerializeField] private PlayerVitals2D playerVitals;
    [SerializeField] private Animator animator;

    private RuntimeAnimatorController healthyController;

    private void Awake()
    {

        if (playerVitals == null)
            playerVitals = GetComponent<PlayerVitals2D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            healthyController = animator.runtimeAnimatorController;
    }

    private void OnEnable()
    {
        if (playerVitals == null)
            return;

        //apply new appearance when infection changes.
        playerVitals.InfectionChanged += ApplyInfectionAppearance;
        ApplyInfectionAppearance(playerVitals.InfectionStage);
    }

    private void OnDisable()
    {

        if (playerVitals != null)
            playerVitals.InfectionChanged -= ApplyInfectionAppearance;
    }

    private void ApplyInfectionAppearance(int stageIndex)
    {
        if (animator == null || playerVitals == null)
            return;

        //get current infection stage and use appropriate sprites
        InfectionProfile.InfectionStage stage =
            playerVitals.Profile != null
                ? playerVitals.Profile.GetStage(stageIndex)
                : InfectionProfile.GetDefaultStage(stageIndex);

        animator.runtimeAnimatorController =
            stage.animationOverride != null
                ? stage.animationOverride
                : healthyController;
    }
}