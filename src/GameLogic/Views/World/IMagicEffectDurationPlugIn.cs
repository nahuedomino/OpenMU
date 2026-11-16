// <copyright file="IMagicEffectDurationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view which shows how long the player's own magic effects (buffs and debuffs) last.
/// </summary>
public interface IMagicEffectDurationPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the remaining duration of the player's own magic effect, which just started or was cast again.
    /// </summary>
    /// <param name="effect">The effect.</param>
    ValueTask ShowMagicEffectDurationAsync(MagicEffect effect);
}
