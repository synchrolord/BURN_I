using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ClimbArea2D : MonoBehaviour
{
    // Count contacts because a player can have more than one collider.
    private readonly Dictionary<PlayerMotor2D, int> occupants = new Dictionary<PlayerMotor2D, int>();

    // Unity calls Reset when this component is first added in the Inspector.
    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMotor2D motor = other.GetComponentInParent<PlayerMotor2D>();
        if (motor == null)
        {
            return;
        }
        occupants.TryGetValue(motor, out int contacts);
        occupants[motor] = contacts + 1;
        motor.EnterClimbArea(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerMotor2D motor = other.GetComponentInParent<PlayerMotor2D>();
        if (motor == null || !occupants.TryGetValue(motor, out int contacts))
        {
            return;
        }
        if (contacts > 1)
        {
            occupants[motor] = contacts - 1;
        }
        else
        {
            occupants.Remove(motor);
            motor.ExitClimbArea(this);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Reconnect a player whose movement component was disabled and enabled on the ladder.
        PlayerMotor2D motor = other.GetComponentInParent<PlayerMotor2D>();
        if (motor != null && occupants.ContainsKey(motor))
        {
            motor.EnterClimbArea(this);
        }
    }

    private void OnDisable()
    {
        foreach (PlayerMotor2D motor in occupants.Keys)
        {
            if (motor != null)
            {
                motor.ExitClimbArea(this);
            }
        }
        occupants.Clear();
    }
}
