// <copyright file="AreaSkillAttackContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Threading;

/// <summary>
/// The area skill and explicit target of the current asynchronous attack flow.
/// Attack restriction plugins can distinguish the aimed hit from incidental area hits.
/// </summary>
public sealed class AreaSkillAttackContext : IDisposable
{
    private static readonly AsyncLocal<AreaSkillAttackContext?> CurrentContext = new();

    private readonly AreaSkillAttackContext? _previous;

    /// <summary>
    /// Initializes a new instance of the <see cref="AreaSkillAttackContext"/> class.
    /// </summary>
    /// <param name="skill">The attacking skill.</param>
    /// <param name="explicitTargetId">The explicitly selected target, or 0xFFFF if none.</param>
    public AreaSkillAttackContext(Skill skill, ushort explicitTargetId)
    {
        this.Skill = skill;
        this.ExplicitTargetId = explicitTargetId;
        this._previous = CurrentContext.Value;
        CurrentContext.Value = this;
    }

    /// <summary>
    /// Gets the current attack context, if an area skill is being performed.
    /// </summary>
    public static AreaSkillAttackContext? Current => CurrentContext.Value;

    /// <summary>
    /// Gets the skill.
    /// </summary>
    public Skill Skill { get; }

    /// <summary>
    /// Gets the explicitly selected target identifier.
    /// </summary>
    public ushort ExplicitTargetId { get; }

    /// <inheritdoc />
    public void Dispose() => CurrentContext.Value = this._previous;
}
