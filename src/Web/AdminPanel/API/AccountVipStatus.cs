// <copyright file="AccountVipStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>The VIP status of an account.</summary>
/// <param name="IsVip">Whether the account is VIP now.</param>
/// <param name="IsPermanent">Whether the VIP status doesn't expire.</param>
/// <param name="ValidUntil">Until when (UTC) the account is VIP by grants; <c>null</c>, if it isn't or it's permanent.</param>
public sealed record AccountVipStatus(bool IsVip, bool IsPermanent, DateTime? ValidUntil);
