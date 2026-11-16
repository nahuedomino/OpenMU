// <copyright file="MagicEffectDurationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// MU Volta: tells the client how long the player's own magic effect lasts, so it can show a countdown on the buff
/// icon. OpenMU's effect status packet has no time, and the item effect packet only covers consumable buffs.
/// </summary>
/// <remarks>
/// Packet: C1 07 FA 01, effect number (1 byte), remaining seconds (2 bytes, little endian; 0xFFFF = unknown or
/// longer). Clients which don't know the code ignore it.
/// </remarks>
[PlugIn]
[Display(Name = "MU Volta magic effect duration", Description = "Sends the remaining duration of the player's own buffs, for the client's buff countdown.")]
[Guid("3F6A1C2E-8B4D-4E7A-9C15-6D2B8E0F4A73")]
[MinimumClient(0, 90, ClientLanguage.Invariant)]
public class MagicEffectDurationPlugIn : IMagicEffectDurationPlugIn
{
    /// <summary>
    /// The size of the packet.
    /// </summary>
    public const int PacketSize = 7;

    /// <summary>
    /// The packet code.
    /// </summary>
    public const byte Code = 0xFA;

    /// <summary>
    /// The packet sub code.
    /// </summary>
    public const byte SubCode = 0x01;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagicEffectDurationPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public MagicEffectDurationPlugIn(RemotePlayer player) => this._player = player;

    /// <summary>
    /// Writes the packet.
    /// </summary>
    /// <param name="span">The target span of at least <see cref="PacketSize"/> bytes.</param>
    /// <param name="effectNumber">The effect number.</param>
    /// <param name="duration">The remaining duration.</param>
    public static void Write(Span<byte> span, byte effectNumber, TimeSpan duration)
    {
        var seconds = duration <= TimeSpan.Zero || duration.TotalSeconds >= ushort.MaxValue
            ? ushort.MaxValue
            : (ushort)Math.Ceiling(duration.TotalSeconds);
        span[0] = 0xC1;
        span[1] = PacketSize;
        span[2] = Code;
        span[3] = SubCode;
        span[4] = effectNumber;
        span[5] = (byte)(seconds & 0xFF);
        span[6] = (byte)(seconds >> 8);
    }

    /// <inheritdoc/>
    public async ValueTask ShowMagicEffectDurationAsync(MagicEffect effect)
    {
        if (this._player.Connection is not { Connected: true } connection
            || effect.Definition.Number <= 0
            || effect.Definition.Number > byte.MaxValue)
        {
            return;
        }

        var effectNumber = (byte)effect.Definition.Number;
        var duration = effect.Duration;

        int WritePacket()
        {
            Write(connection.Output.GetSpan(PacketSize), effectNumber, duration);
            return PacketSize;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }
}
