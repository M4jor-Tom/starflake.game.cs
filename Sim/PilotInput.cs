using System.Collections.Generic;

namespace Starflake.Sim;

/// <summary>The hold actions that are down during one step, by action id.</summary>
public sealed record PilotInput(IReadOnlySet<string> Held)
{
    public static readonly PilotInput None = new(new HashSet<string>());

    public bool IsHeld(string actionId) => Held.Contains(actionId);

    /// <returns>-1, 0 or 1; both held is 0.</returns>
    public float Axis(string negativeId, string positiveId) => (IsHeld(positiveId) ? 1 : 0) - (IsHeld(negativeId) ? 1 : 0);
}
