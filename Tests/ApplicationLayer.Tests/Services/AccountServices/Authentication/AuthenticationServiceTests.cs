using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ApplicationLayer.DtoModels.TokenDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.Authentication;
using DomainLayer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.Authentication
{
    public class AuthenticationServiceTests
    {
        private static Mock<UserManager<Customer>> CreateUserManagerMock()
        {
            var store = new Mock<IUserStore<Customer>>();
            var options = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
            options.Setup(o => o.Value).Returns(new IdentityOptions());
            var passwordHasher = new Mock<IPasswordHasher<Customer>>();
            var userValidators = Array.Empty<IUserValidator<Customer>>();
            var passwordValidators = Array.Empty<IPasswordValidator<Customer>>();
            var keyNormalizer = new Mock<ILookupNormalizer>();
            var errors = new IdentityErrorDescriber();
            var services = new Mock<IServiceProvider>();
            var logger = new Mock<ILogger<UserManager<Customer>>>();

            return new Mock<UserManager<Customer>>(
                store.Object,
                options.Object,
                passwordHasher.Object,
                userValidators,
                passwordValidators,
                keyNormalizer.Object,
                errors,
                services.Object,
                logger.Object
            );
        }

        private static AuthenticationService CreateSut(
            Mock<IHttpContextAccessor> httpContextAccessor,
            Mock<ILogger<AuthenticationService>> logger,
            Mock<UserManager<Customer>> userManager,
            Mock<IRefreshTokenService> refreshTokenService,
            Mock<ITokenService> tokenService)
        {
            var inMemorySettings = new Dictionary<string, string> {
                {"Security:LockoutPolicy:MaxFailedAttempts", "7"},
                {"Security:LockoutPolicy:LockoutDurationMinutes", "20"},
                {"Security:LockoutPolicy:PermanentLockoutAfterAttempts", "12"},
};

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();


            return new AuthenticationService(
                httpContextAccessor.Object,
                logger.Object,
                userManager.Object,
                refreshTokenService.Object,
                tokenService.Object,
                Mock.Of<ApplicationLayer.Services.EmailServices.IErrorNotificationService>(),
                Mock.Of<ApplicationLayer.Services.EmailServices.IAccountEmailService>(),
                configuration
            );
        }

        [Fact]
        public async Task LoginAsync_EmailNotFound_Returns400()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            userManager.Setup(x => x.FindByEmailAsync("missing@ex.com")).ReturnsAsync((Customer)null);

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LoginAsync("missing@ex.com", "pwd");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            userManager.Verify(x => x.IsLockedOutAsync(It.IsAny<Customer>()), Times.Never);
            userManager.Verify(x => x.CheckPasswordAsync(It.IsAny<Customer>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_UserLockedOut_Returns403()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var user = new Customer { Id = "u1", Email = "e@x.com", LockoutEnabled = true };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LoginAsync(user.Email, "pwd");

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
            userManager.Verify(x => x.CheckPasswordAsync(It.IsAny<Customer>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_InvalidPassword_Returns401()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
       
            var http = new Mock<IHttpContextAccessor>();

            var user = new Customer { Id = "u2", Email = "e2@x.com", LockoutEnabled = true };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            userManager.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);
            userManager.Setup(x => x.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.GetAccessFailedCountAsync(user)).ReturnsAsync(1);
          
            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LoginAsync(user.Email, "bad");

            Assert.False(result.Success);
            Assert.Contains(result.StatusCode, new[] { 401, 403 });
            userManager.Verify(x => x.AccessFailedAsync(user), Times.Once);
            userManager.Verify(x => x.GetAccessFailedCountAsync(user), Times.Once);
            token.Verify(x => x.GenerateTokenAsync(It.IsAny<Customer>()), Times.Never);
            refresh.Verify(x => x.GenerateRefreshTokenAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_DeletedUser_Returns400()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var user = new Customer { Id = "u3", Email = "e3@x.com", LockoutEnabled = true, DeletedAt = DateTime.UtcNow };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            userManager.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);
            userManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LoginAsync(user.Email, "pwd");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            token.Verify(x => x.GenerateTokenAsync(It.IsAny<Customer>()), Times.Never);
            refresh.Verify(x => x.GenerateRefreshTokenAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_TokenGenerationFails_Returns500()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var user = new Customer { Id = "u4", Email = "e4@x.com", LockoutEnabled = true };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            userManager.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);
            userManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);

            token.Setup(x => x.GenerateTokenAsync(user)).ReturnsAsync(Result<string>.Fail("err"));

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LoginAsync(user.Email, "pwd");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            refresh.Verify(x => x.GenerateRefreshTokenAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_Success_ReturnsTokenAndRoles()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var context = new DefaultHttpContext();
            http.Setup(x => x.HttpContext).Returns(context);

            var user = new Customer { Id = "u5", Email = "e5@x.com", LockoutEnabled = false };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            userManager.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);
            userManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User", "Admin" });

            token.Setup(x => x.GenerateTokenAsync(user)).ReturnsAsync(Result<string>.Ok("tok"));
            refresh.Setup(x => x.GenerateRefreshTokenAsync(user.Id)).ReturnsAsync(Result<string>.Ok("rt"));

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LoginAsync(user.Email, "pwd");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.NotNull(result.Data);
            Assert.Equal("tok", result.Data.Token);
            Assert.Equal(2, result.Data.Roles.Count);
            userManager.Verify(x => x.UpdateAsync(It.Is<Customer>(c => c.LockoutEnabled)), Times.Once);
            refresh.Verify(x => x.GenerateRefreshTokenAsync(user.Id), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_UserNotFound_Returns401()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var context = new DefaultHttpContext();
            http.Setup(x => x.HttpContext).Returns(context);

            userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((Customer)null);

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LogoutAsync("missing");

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
            userManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<Customer>()), Times.Never);
        }

        [Fact]
        public async Task LogoutAsync_Success_UpdatesSecurityStamp()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var context = new DefaultHttpContext();
            http.Setup(x => x.HttpContext).Returns(context);

            var customer = new Customer { Id = "u6" };
            userManager.Setup(x => x.FindByIdAsync(customer.Id)).ReturnsAsync(customer);
            userManager.Setup(x => x.UpdateSecurityStampAsync(customer)).ReturnsAsync(IdentityResult.Success);

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.LogoutAsync(customer.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            userManager.Verify(x => x.UpdateSecurityStampAsync(customer), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_NoCookie_Returns401()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var context = new DefaultHttpContext();
            http.Setup(x => x.HttpContext).Returns(context);

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.RefreshTokenAsync();

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
            refresh.Verify(x => x.RefreshTokenAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RefreshTokenAsync_FailedRefresh_RemovesTokenAndReturns401()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var context = new DefaultHttpContext();
            context.Request.Headers["Cookie"] = "Refresh=badtoken";
            http.Setup(x => x.HttpContext).Returns(context);

            refresh.Setup(x => x.RefreshTokenAsync("badtoken")).ReturnsAsync(Result<string>.Fail("bad"));
            refresh.Setup(x => x.RemoveRefreshTokenAsync("badtoken")).ReturnsAsync(Result<bool>.Ok(true));

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.RefreshTokenAsync();

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
            refresh.Verify(x => x.RemoveRefreshTokenAsync("badtoken"), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_Success_Returns200WithToken()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            var context = new DefaultHttpContext();
            context.Request.Headers["Cookie"] = "Refresh=oktoken";
            http.Setup(x => x.HttpContext).Returns(context);

            refresh.Setup(x => x.RefreshTokenAsync("oktoken")).ReturnsAsync(Result<string>.Ok("newtoken"));

            var sut = CreateSut(http, logger, userManager, refresh, token);
            var result = await sut.RefreshTokenAsync();

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("newtoken", result.Data.Token);
        }

        [Fact]
        public async Task RemoveRefreshTokenAsync_ServiceThrows_DoesNotPropagate()
        {
            var logger = new Mock<ILogger<AuthenticationService>>();
            var userManager = CreateUserManagerMock();
            var refresh = new Mock<IRefreshTokenService>();
            var token = new Mock<ITokenService>();
            var config = new Mock<IConfiguration>();
            var http = new Mock<IHttpContextAccessor>();

            refresh.Setup(x => x.RemoveRefreshTokenAsync("boom")).ThrowsAsync(new Exception("boom"));

            var sut = CreateSut(http, logger, userManager, refresh, token);
            await sut.RemoveRefreshTokenAsync("boom");

            refresh.Verify(x => x.RemoveRefreshTokenAsync("boom"), Times.Once);
        }
    }
}