// <copyright file="AccountAuthenticationRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.ComponentModel.DataAnnotations;

/// <summary>The password to check; it's never logged.</summary>
public sealed class AccountAuthenticationRequest
{
    /// <summary>Gets or sets the password.</summary>
    [Required]
    [StringLength(20, MinimumLength = 1)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
