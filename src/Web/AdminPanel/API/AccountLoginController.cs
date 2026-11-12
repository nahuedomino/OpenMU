// <copyright file="AccountLoginController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// API for a website on which players log in with their game account: checks a login name and password
/// the same way the game's login does, and reads the VIP status of an account.
/// </summary>
/// <remarks>
/// The caller is a server side application which limits the attempts of its visitors (per address and per
/// account). The password is never logged or audited.
/// </remarks>
[ApiController]
[Route("api/accounts/{loginName}")]
public class AccountLoginController : ControllerBase
{
    private readonly IPersistenceContextProvider _persistenceContextProvider;
    private readonly IDataSource<GameConfiguration> _gameConfigurationSource;
    private readonly ILogger<AccountLoginController> _logger;

    /// <summary>Initializes a new instance of the <see cref="AccountLoginController"/> class.</summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="gameConfigurationSource">The game configuration source.</param>
    /// <param name="logger">The logger.</param>
    public AccountLoginController(IPersistenceContextProvider persistenceContextProvider, IDataSource<GameConfiguration> gameConfigurationSource, ILogger<AccountLoginController> logger)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this._gameConfigurationSource = gameConfigurationSource;
        this._logger = logger;
    }

    /// <summary>
    /// Checks the password of an account, like the game's login does.
    /// </summary>
    /// <param name="loginName">The login name (case sensitive, like in the game).</param>
    /// <param name="request">The password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// 200 with <c>isValid: true</c> and the state of the account, when the password is correct (a banned account is
    /// still reported, so the caller can tell the player why); 200 with <c>isValid: false</c>, when the account doesn't
    /// exist or the password is wrong. Not 401, which would look like a rejected API key.
    /// </returns>
    [HttpPost("authenticate")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AuthenticateAsync(string loginName, [FromBody] AccountAuthenticationRequest request, CancellationToken cancellationToken)
    {
        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        var state = await context.AuthenticateAsync(loginName, request.Password, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            this._logger.LogInformation("API login check failed for account {LoginName}.", loginName);
            return this.Ok(new AccountAuthenticationResult(false, null, null));
        }

        return this.Ok(new AccountAuthenticationResult(true, loginName, state));
    }

    /// <summary>
    /// Gets the VIP status of an account: the "Is VIP" attribute, permanently or granted until a date.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>200 with the status; 404, if the account doesn't exist.</returns>
    [HttpGet("vip")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedCashShop)]
    public async Task<IActionResult> GetVipAsync(string loginName, CancellationToken cancellationToken)
    {
        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.NotFound();
        }

        return this.Ok(GetVipStatus(account, DateTime.UtcNow));
    }

    /// <summary>Gets the VIP status of an account at the specified time.</summary>
    /// <param name="account">The account.</param>
    /// <param name="utcNow">The current time (UTC).</param>
    /// <returns>The VIP status.</returns>
    internal static AccountVipStatus GetVipStatus(Account account, DateTime utcNow)
    {
        var vipId = Stats.IsVip.Id;
        var permanent = account.Attributes.Any(a => a.Definition?.Id == vipId && a.Value > 0)
                        || account.AttributeGrants.Any(g => g.Attribute?.Id == vipId && g.Value > 0 && g.ValidUntil is null);
        var until = account.AttributeGrants
            .Where(g => g.Attribute?.Id == vipId && g.Value > 0 && g.ValidUntil is not null && g.IsValidAt(utcNow))
            .Max(g => g.ValidUntil);
        return new AccountVipStatus(permanent || until is not null, permanent, permanent ? null : until);
    }

    private async ValueTask<IPlayerContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var gameConfiguration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        return this._persistenceContextProvider.CreateNewPlayerContext(gameConfiguration);
    }
}
