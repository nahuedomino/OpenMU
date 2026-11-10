// <copyright file="AreaSkillPvpContextTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests area attack metadata through the complete player-versus-player hit path.
/// </summary>
[TestFixture]
public class AreaSkillPvpContextTests
{
    /// <summary>
    /// Both configured hits reach the aimed player and expose their attack context to restrictions.
    /// </summary>
    /// <param name="skillNumber">The Flame Strike skill or master variant.</param>
    [TestCase(236)]
    [TestCase(492)]
    [TestCase(494)]
    [TestCase(9)]
    [TestCase(39)]
    public async Task AimedAreaSkillDealsTwoPvpHitsAsync(short skillNumber)
    {
        await using var attacker = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var gameContext = attacker.GameContext;
        gameContext.Configuration.AreaSkillHitsPlayer = true;
        gameContext.Configuration.Maps.Single().BattleZone = new BattleZoneDefinition();
        await using var defender = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        attacker.Position = new Point(10, 10);
        defender.Position = new Point(11, 10);
        attacker.Attributes!.AddElement(new ConstValueAttribute(1, Stats.AttackRatePvp), Stats.AttackRatePvp);
        var skill = new BasicModel.Skill
        {
            Number = skillNumber,
            Range = 3,
            AttackDamage = 10,
            DamageType = DamageType.Physical,
            SkillType = SkillType.AreaSkillAutomaticHits,
            NumberOfHitsPerAttack = 2,
        };
        await attacker.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
        var restriction = new ExplicitTargetRestriction();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerAttackRestrictionPlugIn>(restriction);
        var healthBefore = defender.Attributes![Stats.CurrentHealth];

        await new AreaSkillAttackAction().AttackAsync(
            attacker, defender.Id, (ushort)skillNumber, defender.Position, 0).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(defender.Attributes[Stats.CurrentHealth], Is.LessThan(healthBefore));
            Assert.That(restriction.Contexts, Has.Count.EqualTo(2));
            Assert.That(restriction.Contexts.All(context => context.Skill == skill && context.ExplicitTargetId == defender.Id), Is.True);
            Assert.That(AreaSkillAttackContext.Current, Is.Null);
        });
        Mock.Get(defender.ViewPlugIns.GetPlugIn<IShowHitPlugIn>()!)
            .Verify(view => view.ShowHitAsync(defender, It.Is<HitInfo>(hit => hit.TotalDamage > 0)), Times.Exactly(2));
    }

    /// <summary>
    /// Explicit-hit packets retain the target nominated by the cast, not the current hit target.
    /// </summary>
    /// <param name="aimed">Whether the cast nominated the defender.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task ExplicitHitsUseCastTargetAsync(bool aimed)
    {
        await using var attacker = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var gameContext = attacker.GameContext;
        gameContext.Configuration.AreaSkillHitsPlayer = true;
        gameContext.Configuration.Maps.Single().BattleZone = new BattleZoneDefinition();
        await using var defender = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        attacker.Position = new Point(10, 10);
        defender.Position = new Point(11, 10);
        attacker.Attributes!.AddElement(new ConstValueAttribute(1, Stats.AttackRatePvp), Stats.AttackRatePvp);
        var skill = new BasicModel.Skill
        {
            Number = 9,
            Range = 5,
            AttackDamage = 10,
            DamageType = DamageType.Physical,
            SkillType = SkillType.AreaSkillExplicitHits,
        };
        await attacker.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
        var restriction = new ExplicitTargetRestriction();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerAttackRestrictionPlugIn>(restriction);
        var healthBefore = defender.Attributes![Stats.CurrentHealth];
        await new AreaSkillAttackAction().AttackAsync(
            attacker, aimed ? defender.Id : ushort.MaxValue, 9, defender.Position, 0).ConfigureAwait(false);

        await new AreaSkillHitAction().AttackTargetAsync(
            attacker, defender, attacker.SkillList.GetSkill(9)!).ConfigureAwait(false);

        Assert.That(restriction.Contexts, Has.Count.EqualTo(1));
        Assert.That(restriction.Contexts[0].ExplicitTargetId, Is.EqualTo(aimed ? defender.Id : ushort.MaxValue));
        Assert.That(
            defender.Attributes[Stats.CurrentHealth],
            aimed ? Is.LessThan(healthBefore) : Is.EqualTo(healthBefore));
        Assert.That(AreaSkillAttackContext.Current, Is.Null);
    }

    [Guid("10BD9C60-C330-49E7-ACD3-1AD9C2DBF3F4")]
    private sealed class ExplicitTargetRestriction : IPlayerAttackRestrictionPlugIn
    {
        public List<AreaSkillAttackContext> Contexts { get; } = new();

        public void CheckAttack(Player attacker, Player defender, CancelEventArgs eventArgs)
        {
            if (AreaSkillAttackContext.Current is { } context)
            {
                this.Contexts.Add(context);
                eventArgs.Cancel |= context.ExplicitTargetId != defender.Id;
            }
            else
            {
                eventArgs.Cancel = true;
            }
        }
    }
}
