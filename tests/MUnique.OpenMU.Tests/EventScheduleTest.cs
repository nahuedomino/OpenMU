// <copyright file="EventScheduleTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameServer.MessageHandler.MuVolta;

/// <summary>
/// Tests the event schedule of the MU Volta client's event window (H key).
/// </summary>
[TestFixture]
public class EventScheduleTest
{
    private static readonly DateTime Now = new(2026, 11, 16, 1, 10, 0, DateTimeKind.Utc);

    /// <summary>
    /// A run which is going on now is listed with its start, then the starts of the next 24 hours.
    /// </summary>
    [Test]
    public void StartsIncludeTheRunningOneAndTheNext24Hours()
    {
        var configuration = new PeriodicTaskConfiguration
        {
            TaskDuration = TimeSpan.FromMinutes(20),
            Timetable = [new TimeOnly(1, 0), new TimeOnly(5, 0)],
        };

        var starts = EventSchedule.GetStarts(configuration, Now, TimeZoneInfo.Utc);

        Assert.That(starts, Is.EqualTo(new[]
        {
            new DateTime(2026, 11, 16, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc),
        }));
    }

    /// <summary>
    /// The timetable is in the server's time zone (Argentina: UTC-3).
    /// </summary>
    [Test]
    public void TheTimetableIsInTheServerTimeZone()
    {
        var argentina = TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART");
        var configuration = new PeriodicTaskConfiguration { TaskDuration = TimeSpan.FromMinutes(10), Timetable = [new TimeOnly(0, 0)] };

        var starts = EventSchedule.GetStarts(configuration, Now, argentina);

        Assert.That(starts, Is.EqualTo(new[] { new DateTime(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc) }));
    }

    /// <summary>
    /// Events of other game servers are left out; the next one comes first; the names are the players' names.
    /// </summary>
    [Test]
    public void BuildFiltersByServerAndOrdersByStart()
    {
        var bloodCastle = new PeriodicTaskConfiguration { TaskDuration = TimeSpan.FromMinutes(20), Timetable = [new TimeOnly(9, 0)] };
        var happyHour = new PeriodicTaskConfiguration { TaskDuration = TimeSpan.FromMinutes(60), Timetable = [new TimeOnly(2, 0)] };
        var otherServer = new PeriodicTaskConfiguration { TaskDuration = TimeSpan.FromMinutes(5), Timetable = [new TimeOnly(1, 30)], AllowedServerIds = [1] };

        var events = EventSchedule.Build(
            [(typeof(BloodCastleStartPlugIn), bloodCastle), (typeof(HappyHourPlugIn), happyHour), (typeof(KanturuStartPlugIn), otherServer)],
            0,
            Now,
            TimeZoneInfo.Utc);

        Assert.That(events.Select(e => e.Name), Is.EqualTo(new[] { "Happy Hour", "Blood Castle" }));
        Assert.That(events[0].Duration, Is.EqualTo(TimeSpan.FromMinutes(60)));
    }

    /// <summary>
    /// The configuration of the event plugins is found by reflection, also on the mini game and invasion plugins.
    /// </summary>
    /// <param name="plugInType">The plugin type.</param>
    [TestCase(typeof(BloodCastleStartPlugIn))]
    [TestCase(typeof(HappyHourPlugIn))]
    [TestCase(typeof(KanturuStartPlugIn))]
    [TestCase(typeof(GameLogic.PlugIns.InvasionEvents.RedDragonInvasionPlugIn))]
    public void TheConfigurationOfAnEventPlugInIsFound(Type plugInType)
    {
        var property = plugInType.GetProperty("Configuration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        Assert.That(typeof(PeriodicTaskConfiguration).IsAssignableFrom(property!.PropertyType), Is.True);
        Assert.That(typeof(IScheduledPlugIn).IsAssignableFrom(plugInType), Is.True);
    }

    /// <summary>
    /// The packet: C2 header, FA 02, count, then per event the name, duration and the starts in seconds from now.
    /// </summary>
    [Test]
    public void PacketCarriesNamesDurationsAndSecondsFromNow()
    {
        var events = new[] { new ScheduledEvent("Kanturu", TimeSpan.FromMinutes(20), [Now.AddMinutes(-10), Now.AddHours(1)]) };

        var packet = EventSchedule.WritePacket(events, Now);

        var expected = new List<byte> { 0xC2, 0x00, 0x00, 0xFA, 0x02, 0x01, 0x07 };
        expected.AddRange("Kanturu"u8.ToArray());
        expected.AddRange(new byte[] { 20, 0, 2 });
        expected.AddRange(BitConverter.GetBytes(-600));
        expected.AddRange(BitConverter.GetBytes(3600));
        expected[2] = (byte)expected.Count;
        Assert.That(packet, Is.EqualTo(expected.ToArray()));
    }
}
