// <copyright file="ReplacementExampleStrategyPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.Tests;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

/// <summary>
/// A strategy plugin with the same key as <see cref="ExampleStrategyPlugIn"/>, which replaces it.
/// </summary>
[Guid("5D0F6B2E-3A41-4C8F-9E27-B6A1C0D4E853")]
[PlugIn]
[Display(Name = nameof(ReplacementExampleStrategyPlugIn), Description = "A test strategy plugin which replaces another one.")]
public class ReplacementExampleStrategyPlugIn : IExampleStrategyPlugIn
{
    /// <inheritdoc />
    public string Key => ExampleStrategyPlugIn.CommandKey;

    /// <inheritdoc/>
    public void HandleCommand(string command)
    {
    }
}
