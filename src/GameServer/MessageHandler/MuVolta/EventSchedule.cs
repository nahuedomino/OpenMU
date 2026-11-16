// <copyright file="EventSchedule.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.MuVolta;

using System.Buffers.Binary;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// An event in the schedule which the client shows on the H key: its name, how long it runs and when it starts.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Duration">How long one run lasts.</param>
/// <param name="StartsUtc">The starts within the schedule's window, in UTC; the first may be in the past while it runs.</param>
public sealed record ScheduledEvent(string Name, TimeSpan Duration, IReadOnlyList<DateTime> StartsUtc);

/// <summary>
/// The schedule of the periodic events (Blood Castle, Devil Square, Happy Hour, invasions...) for the client's
/// event window, and the packet which carries it.
/// </summary>
/// <remarks>
/// Request (client): C1 04 FA 02. Response: C2, length (2 bytes, big endian), FA 02, event count; per event: name
/// length (1 byte) and UTF-8 name, duration in minutes (2 bytes, little endian), start count (1 byte) and per start
/// the seconds from now (4 bytes, little endian, signed; negative while the event runs).
/// </remarks>
public static class EventSchedule
{
    /// <summary>The packet code of the request and the response.</summary>
    public const byte Code = 0xFA;

    /// <summary>The sub code of the request and the response.</summary>
    public const byte SubCode = 0x02;

    /// <summary>How far ahead the starts are listed.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private const int MaximumEvents = 32;
    private const int MaximumStartsPerEvent = 48;
    private const int MaximumNameBytes = 48;

    private static readonly Dictionary<string, string> Names = new()
    {
        ["BloodCastleStartPlugIn"] = "Blood Castle",
        ["ChaosCastleStartPlugIn"] = "Chaos Castle",
        ["DevilSquareStartPlugIn"] = "Devil Square",
        ["IllusionTempleStartPlugIn"] = "Illusion Temple",
        ["KanturuStartPlugIn"] = "Kanturu",
        ["HappyHourPlugIn"] = "Happy Hour",
        ["WanderingMerchantsPlugIn"] = "Wandering Merchants",
    };

    /// <summary>
    /// Gets the starts of a timetable within the window, including the run which is going on now.
    /// </summary>
    /// <param name="configuration">The event's configuration.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the timetable.</param>
    /// <returns>The starts, in UTC, in order.</returns>
    public static IReadOnlyList<DateTime> GetStarts(PeriodicTaskConfiguration configuration, DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        var starts = new List<DateTime>();
        var end = utcNow + Window;

        // From one run back, so that a run which is going on now is listed, too.
        var from = utcNow - configuration.TaskDuration - TimeSpan.FromTicks(1);
        while (starts.Count < MaximumStartsPerEvent
               && configuration.GetNextStartUtc(from, serverTimeZone) is { } start
               && start <= end)
        {
            if (start + configuration.TaskDuration > utcNow)
            {
                starts.Add(start);
            }

            from = start;
        }

        return starts;
    }

    /// <summary>
    /// Gets the schedule of the events which run on a game server.
    /// </summary>
    /// <param name="events">The active events: plugin type and configuration.</param>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the timetables.</param>
    /// <returns>The events with starts in the window, the next one first.</returns>
    public static IReadOnlyList<ScheduledEvent> Build(IEnumerable<(Type PlugInType, PeriodicTaskConfiguration Configuration)> events, byte serverId, DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        return events
            .Where(e => e.Configuration.AllowedServerIds.Count == 0 || e.Configuration.AllowedServerIds.Contains(serverId))
            .Select(e => new ScheduledEvent(GetName(e.PlugInType), e.Configuration.TaskDuration, GetStarts(e.Configuration, utcNow, serverTimeZone)))
            .Where(e => e.StartsUtc.Count > 0)
            .OrderBy(e => e.StartsUtc[0])
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .Take(MaximumEvents)
            .ToList();
    }

    /// <summary>
    /// Gets the name the players see for an event plugin.
    /// </summary>
    /// <param name="plugInType">The plugin type.</param>
    /// <returns>The name.</returns>
    public static string GetName(Type plugInType)
    {
        if (Names.TryGetValue(plugInType.Name, out var known))
        {
            return known;
        }

        var name = plugInType.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                   ?? plugInType.GetCustomAttribute<DisplayAttribute>()?.GetName()
                   ?? plugInType.Name;
        const string muVoltaInvasion = "MU Volta invasion: ";
        if (name.StartsWith(muVoltaInvasion, StringComparison.Ordinal))
        {
            return name[muVoltaInvasion.Length..] + " invasion";
        }

        return name.EndsWith("PlugIn", StringComparison.Ordinal) ? name[..^"PlugIn".Length] : name;
    }

    /// <summary>
    /// Writes the response packet.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The packet.</returns>
    public static byte[] WritePacket(IReadOnlyList<ScheduledEvent> events, DateTime utcNow)
    {
        var body = new List<byte> { (byte)Math.Min(events.Count, MaximumEvents) };
        foreach (var e in events.Take(MaximumEvents))
        {
            var name = Encoding.UTF8.GetBytes(e.Name);
            if (name.Length > MaximumNameBytes)
            {
                name = name[..MaximumNameBytes];
            }

            body.Add((byte)name.Length);
            body.AddRange(name);
            var minutes = (ushort)Math.Clamp(Math.Round(e.Duration.TotalMinutes), 0, ushort.MaxValue);
            body.Add((byte)(minutes & 0xFF));
            body.Add((byte)(minutes >> 8));
            var starts = e.StartsUtc.Take(MaximumStartsPerEvent).ToList();
            body.Add((byte)starts.Count);
            foreach (var start in starts)
            {
                var seconds = (int)Math.Clamp(Math.Round((start - utcNow).TotalSeconds), int.MinValue, int.MaxValue);
                Span<byte> value = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(value, seconds);
                body.AddRange(value.ToArray());
            }
        }

        var packet = new byte[5 + body.Count];
        packet[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1), (ushort)packet.Length);
        packet[3] = Code;
        packet[4] = SubCode;
        body.CopyTo(packet, 5);
        return packet;
    }
}
