using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Idle()
    {
        animator.Play("Idle");
    }

    public void Run()
    {
        animator.Play("Run");
    }

    public void Jump()
    {
        animator.Play("Jump");
    }

    public void JumpUp()
    {
        animator.Play("JumUp");
    }

    public void JumpFall()
    {
        animator.Play("JumpFall");
    }

    public void Land()
    {
        animator.Play("Land");
    }

    public void Shoot()
    {
        animator.Play("Shoot");
    }

    public void Die()
    {
        animator.Play("Dead");
    }

    public void Tra()
    {
        animator.Play("tra");
    }
}