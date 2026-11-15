// <copyright file="TargetedSkillSplashTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.Pathfinding;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests the splash targets of <see cref="TargetedSkillDefaultPlugin"/> (e.g. Fire Burst, Death Stab).
/// </summary>
[TestFixture]
public class TargetedSkillSplashTest
{
    private IGameContext _gameContext = null!;

    /// <summary>
    /// Sets up a fresh game context before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._gameContext = GameContextTestHelper.CreateGameContext();
        this._gameContext.Configuration.AreaSkillHitsPlayer = true;
    }

    /// <summary>
    /// A summon next to the hunted monster isn't splashed: a hit on it would start self-defense against
    /// its owner, e.g. an offline leveling Dark Lord's Fire Burst hitting the party elf's summon.
    /// </summary>
    [Test]
    public async ValueTask SplashSparesSummonsNextToTheTargetAsync()
    {
        var caster = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var summonOwner = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var target = await this.CreateMonsterAsync(new Point(100, 100)).ConfigureAwait(false);
        var neighbour = await this.CreateMonsterAsync(new Point(101, 100)).ConfigureAwait(false);
        var summon = await this.CreateMonsterAsync(new Point(100, 101), new SummonedMonsterIntelligence(summonOwner)).ConfigureAwait(false);

        var targets = new TargetExposingPlugin().GetTargets(caster, target, CreateFireBurstLikeSkill()).ToList();

        Assert.That(summon.SummonedBy, Is.SameAs(summonOwner));
        Assert.That(targets, Does.Contain(target));
        Assert.That(targets, Does.Contain(neighbour));
        Assert.That(targets, Does.Not.Contain(summon));
    }

    /// <summary>
    /// An explicitly targeted summon is still hit; only the splash spares summons.
    /// </summary>
    [Test]
    public async ValueTask ExplicitlyTargetedSummonIsHitAsync()
    {
        var caster = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var summonOwner = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var summon = await this.CreateMonsterAsync(new Point(100, 101), new SummonedMonsterIntelligence(summonOwner)).ConfigureAwait(false);

        var targets = new TargetExposingPlugin().GetTargets(caster, summon, CreateFireBurstLikeSkill()).ToList();

        Assert.That(targets, Does.Contain(summon));
    }

    private static Skill CreateFireBurstLikeSkill()
    {
        return new TestSkill
        {
            Number = 61,
            Range = 6,
            SkillType = SkillType.DirectHit,
            DamageType = DamageType.Physical,
            Target = SkillTarget.ExplicitWithImplicitInRange,
            ImplicitTargetRange = 1,
        };
    }

    private async ValueTask<Monster> CreateMonsterAsync(Point position, INpcIntelligence? intelligence = null)
    {
        var monsterDefinition = new MonsterDefinition { ObjectKind = NpcObjectKind.Monster };
        var map = await this._gameContext.GetMapAsync(0).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            X1 = position.X,
            Y1 = position.Y,
            X2 = position.X,
            Y2 = position.Y,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            NullDropGenerator.Instance,
            intelligence ?? new Mock<INpcIntelligence>().Object,
            this._gameContext.PlugInManager,
            this._gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }

    private sealed class TargetExposingPlugin : TargetedSkillDefaultPlugin
    {
        public IEnumerable<IAttackable> GetTargets(Player player, IAttackable target, Skill skill)
            => this.DetermineTargets(player, target, skill);
    }

    private sealed class TestSkill : Skill
    {
        public TestSkill()
        {
            this.Requirements = new List<AttributeRequirement>();
            this.ConsumeRequirements = new List<AttributeRequirement>();
        }
    }
}
