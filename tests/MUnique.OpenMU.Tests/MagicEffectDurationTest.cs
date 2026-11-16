// <copyright file="MagicEffectDurationTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.GameServer.RemoteView.World;
using MagicEffectDefinition = MUnique.OpenMU.Persistence.BasicModel.MagicEffectDefinition;

/// <summary>
/// Tests that the player learns how long its own magic effects last (MU Volta buff countdown).
/// </summary>
[TestFixture]
public class MagicEffectDurationTest
{
    /// <summary>
    /// A new effect and a recast which only restarts the running effect's timer both report the duration.
    /// </summary>
    [Test]
    public async ValueTask CastAndRecastReportTheDurationAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        var view = Mock.Get(player.ViewPlugIns.GetPlugIn<IMagicEffectDurationPlugIn>()!);
        var definition = new MagicEffectDefinition { Number = 8 };
        var reported = new List<TimeSpan>();
        view.Setup(v => v.ShowMagicEffectDurationAsync(It.IsAny<MagicEffect>()))
            .Callback<MagicEffect>(e => reported.Add(e.Duration))
            .Returns(ValueTask.CompletedTask);

        try
        {
            await player.MagicEffectList.AddEffectAsync(new MagicEffect(TimeSpan.FromSeconds(60), definition)).ConfigureAwait(false);
            Assert.That(reported, Is.EqualTo(new[] { TimeSpan.FromSeconds(60) }));

            // Same power ups: the running effect is kept, only its timer restarts.
            await player.MagicEffectList.AddEffectAsync(new MagicEffect(TimeSpan.FromSeconds(90), definition)).ConfigureAwait(false);
            Assert.That(reported, Is.EqualTo(new[] { TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90) }));
            Assert.That(player.MagicEffectList.GetActiveEffectsSnapshot().Count, Is.EqualTo(1));
        }
        finally
        {
            await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The packet carries the effect number and the remaining seconds, rounded up.
    /// </summary>
    [Test]
    public void PacketCarriesEffectAndSeconds()
    {
        var span = new byte[MagicEffectDurationPlugIn.PacketSize];
        MagicEffectDurationPlugIn.Write(span, 8, TimeSpan.FromSeconds(299.2));
        Assert.That(span, Is.EqualTo(new byte[] { 0xC1, 0x07, 0xFA, 0x01, 0x08, 0x2C, 0x01 }));

        MagicEffectDurationPlugIn.Write(span, 8, TimeSpan.FromDays(2));
        Assert.That(span[5..], Is.EqualTo(new byte[] { 0xFF, 0xFF }));
    }
}
