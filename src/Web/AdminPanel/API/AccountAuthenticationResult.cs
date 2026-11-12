// <copyright file="AccountAuthenticationResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Text.Json.Serialization;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>The result of a password check.</summary>
/// <param name="IsValid">Whether the account exists and the password is correct.</param>
/// <param name="LoginName">The login name, if the password is correct.</param>
/// <param name="State">The state of the account, e.g. <see cref="AccountState.Banned"/>, if the password is correct.</param>
public sealed record AccountAuthenticationResult(bool IsValid, string? LoginName, [property: JsonConverter(typeof(JsonStringEnumConverter))] AccountState? State);
