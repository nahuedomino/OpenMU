// <copyright file="EventScheduleRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.MuVolta;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// MU Volta: answers the client's event window (H key) with the schedule of the active events of this game server, from their
/// live configuration: Blood Castle, Chaos Castle, Devil Square, Happy Hour, the invasions, and so on.
/// </summary>
[PlugIn]
[Display(Name = "MU Volta event schedule (H key)", Description = "Sends the client the next starts of the active events of this game server (mini games, Happy Hour, invasions) for its event window.")]
[Guid("C5E81A47-3B92-4F0D-A6D8-91E2B7C4F035")]
public class EventScheduleRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(1);

    private static readonly ConditionalWeakTable<Player, StrongBox<DateTime>> LastRequests = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => EventSchedule.Code;

    /// <summary>
    /// Gets the active events with a timetable.
    /// </summary>
    /// <param name="plugInManager">The plugin manager.</param>
    /// <returns>The plugin types and configurations.</returns>
    public static IEnumerable<(Type PlugInType, PeriodicTaskConfiguration Configuration)> GetActiveEvents(PlugInManager plugInManager)
    {
        foreach (var plugIn in plugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<IScheduledPlugIn>())
        {
            var configuration = plugIn.GetType().GetProperty("Configuration", BindingFlags.Public | BindingFlags.Instance)?.GetValue(plugIn) as PeriodicTaskConfiguration;
            if (configuration is { Timetable.Count: > 0 })
            {
                yield return (plugIn.GetType(), configuration);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 4 || packet.Span[3] != EventSchedule.SubCode
            || player is not RemotePlayer { Connection: { Connected: true } connection }
            || player.GameContext is not IGameServerContext gameServer)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var last = LastRequests.GetOrCreateValue(player);
        lock (last)
        {
            if (now - last.Value < MinimumRequestInterval)
            {
                return;
            }

            last.Value = now;
        }

        var events = EventSchedule.Build(GetActiveEvents(player.GameContext.PlugInManager), gameServer.Id, now, player.GameContext.ServerTimeZone);
        var response = EventSchedule.WritePacket(events, now);

        int Write()
        {
            response.CopyTo(connection.Output.GetSpan(response.Length));
            return response.Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
