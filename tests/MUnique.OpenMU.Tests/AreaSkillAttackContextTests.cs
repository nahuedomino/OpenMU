// <copyright file="AreaSkillAttackContextTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the isolation of the metadata used by area PvP restriction plugins.
/// </summary>
[TestFixture]
public class AreaSkillAttackContextTests
{
    /// <summary>
    /// Metadata survives awaits and nested scopes, but does not escape into the caller.
    /// </summary>
    [Test]
    public async ValueTask ContextIsScopedToTheAttackAsync()
    {
        var skill = new BasicModel.Skill { Number = 236 };
        async ValueTask PerformAsync()
        {
            using var outer = new AreaSkillAttackContext(skill, 42);
            await Task.Yield();
            Assert.That(AreaSkillAttackContext.Current, Is.SameAs(outer));
            using (var inner = new AreaSkillAttackContext(skill, 43))
            {
                Assert.That(AreaSkillAttackContext.Current!.ExplicitTargetId, Is.EqualTo(43));
            }

            Assert.That(AreaSkillAttackContext.Current!.ExplicitTargetId, Is.EqualTo(42));
        }

        await PerformAsync().ConfigureAwait(false);
        Assert.That(AreaSkillAttackContext.Current, Is.Null);
    }

    /// <summary>
    /// Concurrent casters do not share their selected target.
    /// </summary>
    [Test]
    public async ValueTask ConcurrentAttacksAreIsolatedAsync()
    {
        async Task PerformAsync(ushort targetId)
        {
            using var context = new AreaSkillAttackContext(new BasicModel.Skill { Number = 236 }, targetId);
            await Task.Yield();
            Assert.That(AreaSkillAttackContext.Current!.ExplicitTargetId, Is.EqualTo(targetId));
        }

        await Task.WhenAll(PerformAsync(42), PerformAsync(43)).ConfigureAwait(false);
        Assert.That(AreaSkillAttackContext.Current, Is.Null);
    }
}
