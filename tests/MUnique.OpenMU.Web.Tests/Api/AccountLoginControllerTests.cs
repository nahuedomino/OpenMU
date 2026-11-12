// <copyright file="AccountLoginControllerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.Api;

using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Tests for the <see cref="AccountLoginController"/>, which checks game passwords for a website.
/// </summary>
[TestFixture]
public class AccountLoginControllerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private InMemoryPersistenceContextProvider _persistenceContextProvider = null!;

    private AccountLoginController _controller = null!;

    /// <summary>
    /// Sets up an in-memory database with a normal and a banned account, and the controller.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._persistenceContextProvider = new InMemoryPersistenceContextProvider();
        var gameConfigurationSource = new Mock<IDataSource<GameConfiguration>>();
        gameConfigurationSource
            .Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameConfiguration());
        this._controller = new AccountLoginController(this._persistenceContextProvider, gameConfigurationSource.Object, Mock.Of<ILogger<AccountLoginController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        using var context = this._persistenceContextProvider.CreateNewPlayerContext(new GameConfiguration());
        var player = context.CreateNew<Account>();
        player.LoginName = "player";
        player.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        var banned = context.CreateNew<Account>();
        banned.LoginName = "banned";
        banned.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        banned.State = AccountState.Banned;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The right password answers with the state of the account.
    /// </summary>
    [Test]
    public async Task CorrectPasswordReturnsStateAsync()
    {
        var result = await this._controller.AuthenticateAsync("player", new AccountAuthenticationRequest { Password = "secret" }, CancellationToken.None).ConfigureAwait(false);

        var value = (AccountAuthenticationResult)((OkObjectResult)result).Value!;
        Assert.That(value.IsValid, Is.True);
        Assert.That(value.LoginName, Is.EqualTo("player"));
        Assert.That(value.State, Is.EqualTo(AccountState.Normal));
        Assert.That(JsonSerializer.Serialize(value), Does.Contain("\"State\":\"Normal\""));
    }

    /// <summary>
    /// A banned account with the right password is reported as banned, so the website can say why it can't log in.
    /// </summary>
    [Test]
    public async Task BannedAccountReturnsBannedStateAsync()
    {
        var result = await this._controller.AuthenticateAsync("banned", new AccountAuthenticationRequest { Password = "secret" }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(((AccountAuthenticationResult)((OkObjectResult)result).Value!).State, Is.EqualTo(AccountState.Banned));
    }

    /// <summary>
    /// A wrong password, a different case of the login name and an unknown account give the same answer,
    /// which isn't 401 (that would look like a rejected API key).
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    [TestCase("player", "wrong")]
    [TestCase("Player", "secret")]
    [TestCase("nobody", "secret")]
    public async Task WrongCredentialsAreNotValidAsync(string loginName, string password)
    {
        var result = await this._controller.AuthenticateAsync(loginName, new AccountAuthenticationRequest { Password = password }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(((OkObjectResult)result).Value, Is.EqualTo(new AccountAuthenticationResult(false, null, null)));
    }

    /// <summary>
    /// The VIP status of an unknown account is 404, of an account without VIP "not VIP".
    /// </summary>
    [Test]
    public async Task VipStatusOfAccountsAsync()
    {
        Assert.That(await this._controller.GetVipAsync("nobody", CancellationToken.None).ConfigureAwait(false), Is.InstanceOf<NotFoundResult>());

        var result = await this._controller.GetVipAsync("player", CancellationToken.None).ConfigureAwait(false);
        Assert.That(((OkObjectResult)result).Value, Is.EqualTo(new AccountVipStatus(false, false, null)));
    }

    /// <summary>
    /// Several grants: the latest valid end date counts; expired grants and other attributes don't.
    /// </summary>
    [Test]
    public void VipUntilIsTheLatestValidGrant()
    {
        var account = new Persistence.BasicModel.Account();
        account.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.IsVip, Value = 1, ValidUntil = Now.AddDays(-1) });
        account.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.IsVip, Value = 1, ValidUntil = Now.AddDays(15) });
        account.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.IsVip, Value = 1, ValidUntil = Now.AddDays(30) });
        account.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.MaximumHealth, Value = 1, ValidUntil = Now.AddDays(60) });

        Assert.That(AccountLoginController.GetVipStatus(account, Now), Is.EqualTo(new AccountVipStatus(true, false, Now.AddDays(30))));
    }

    /// <summary>
    /// Only expired grants: not VIP.
    /// </summary>
    [Test]
    public void ExpiredVipIsNotVip()
    {
        var account = new Persistence.BasicModel.Account();
        account.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.IsVip, Value = 1, ValidUntil = Now });

        Assert.That(AccountLoginController.GetVipStatus(account, Now), Is.EqualTo(new AccountVipStatus(false, false, null)));
    }

    /// <summary>
    /// A permanent attribute or a grant without end date make the account VIP without an end date.
    /// </summary>
    [Test]
    public void PermanentVipHasNoEndDate()
    {
        var byAttribute = new Persistence.BasicModel.Account();
        byAttribute.Attributes.Add(new Persistence.BasicModel.StatAttribute(Stats.IsVip, 1));
        byAttribute.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.IsVip, Value = 1, ValidUntil = Now.AddDays(5) });
        var byGrant = new Persistence.BasicModel.Account();
        byGrant.AttributeGrants.Add(new Persistence.BasicModel.AccountAttributeGrant { Attribute = Stats.IsVip, Value = 1 });

        Assert.That(AccountLoginController.GetVipStatus(byAttribute, Now), Is.EqualTo(new AccountVipStatus(true, true, null)));
        Assert.That(AccountLoginController.GetVipStatus(byGrant, Now), Is.EqualTo(new AccountVipStatus(true, true, null)));
    }
}
