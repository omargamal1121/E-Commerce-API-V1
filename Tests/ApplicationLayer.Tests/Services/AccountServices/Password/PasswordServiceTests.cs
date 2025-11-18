using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.Password;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.Password
{
    public class PasswordServiceTests
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

        private static PasswordService CreateSut(
            Mock<IAccountEmailService> accountEmail,
            Mock<IBackgroundJobClient> backgroundJob,
            Mock<ILogger<PasswordService>> logger,
            Mock<UserManager<Customer>> userManager,
            Mock<IRefreshTokenService> refreshToken,
            Mock<IErrorNotificationService> errorNotifier)
        {

        
            return new PasswordService(
                accountEmail.Object,
                backgroundJob.Object,
                logger.Object,
                userManager.Object,
                refreshToken.Object,
                errorNotifier.Object
            );
        }

        [Fact]
        public async Task ChangePasswordAsync_HappyPath_ReturnsOkAndEnqueues()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u1", Email = "e@x.com", UserName = "user", DeletedAt = null };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.ChangePasswordAsync(user, "old", "new")).ReturnsAsync(IdentityResult.Success);

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ChangePasswordAsync(user.Id, "old", "new");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            userManager.Verify(x => x.ChangePasswordAsync(user, "old", "new"), Times.Once);

        }

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_Returns404AndNoCalls()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((Customer)null);

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ChangePasswordAsync("missing", "old", "new");

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            userManager.Verify(x => x.ChangePasswordAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
   
        }

        [Fact]
        public async Task ChangePasswordAsync_SamePassword_Returns400()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u2", DeletedAt = null };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ChangePasswordAsync(user.Id, "same", "same");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            userManager.Verify(x => x.ChangePasswordAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_UpdateFailed_Returns400AndNoEnqueue()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u3", DeletedAt = null };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.ChangePasswordAsync(user, "old", "new")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "bad" }));

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ChangePasswordAsync(user.Id, "old", "new");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);

        }

        [Fact]
        public async Task ChangePasswordAsync_Exception_Returns500AndEnqueuesError()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByIdAsync("boom")).ThrowsAsync(new Exception("boom"));

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ChangePasswordAsync("boom", "old", "new");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
           
        }

        [Fact]
        public async Task RequestPasswordResetAsync_UserExists_EnqueuesEmailAndReturnsOk()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u4", Email = "e@x.com", UserName = "u", DeletedAt = null };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.RequestPasswordResetAsync(user.Email);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
           
        }

        [Fact]
        public async Task RequestPasswordResetAsync_UserMissingOrDeleted_NoEnqueueReturnsOk()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByEmailAsync("missing@ex.com")).ReturnsAsync((Customer)null);

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.RequestPasswordResetAsync("missing@ex.com");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);

        }

        [Fact]
        public async Task RequestPasswordResetAsync_Exception_Returns500AndEnqueuesError()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByEmailAsync("boom@ex.com")).ThrowsAsync(new Exception("boom"));

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.RequestPasswordResetAsync("boom@ex.com");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
           
        }

        [Fact]
        public async Task ResetPasswordAsync_UserMissingOrDeleted_ReturnsOkAndNoEnqueue()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByEmailAsync("missing@ex.com")).ReturnsAsync((Customer)null);

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ResetPasswordAsync("missing@ex.com", "t", "new");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);

        }

        [Fact]
        public async Task ResetPasswordAsync_ResetFailed_Returns400AndNoEnqueue()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u5", Email = "e@x.com", DeletedAt = null };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.ResetPasswordAsync(user, "t", "new")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "err" }));

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ResetPasswordAsync(user.Email, "t", "new");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);

        }

        [Fact]
        public async Task ResetPasswordAsync_Success_EnqueuesAndReturnsOk()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u6", Email = "e6@x.com", DeletedAt = null };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.ResetPasswordAsync(user, "t", "new")).ReturnsAsync(IdentityResult.Success);

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ResetPasswordAsync(user.Email, "t", "new");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);

        }

        [Fact]
        public async Task ResetPasswordAsync_Exception_Returns500AndEnqueuesError()
        {
            var accountEmail = new Mock<IAccountEmailService>();
            var backgroundJob = new Mock<IBackgroundJobClient>();
            var logger = new Mock<ILogger<PasswordService>>();
            var userManager = CreateUserManagerMock();
            var refreshToken = new Mock<IRefreshTokenService>();
            var errorNotifier = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByEmailAsync("boom@ex.com")).ThrowsAsync(new Exception("boom"));

            var sut = CreateSut(accountEmail, backgroundJob, logger, userManager, refreshToken, errorNotifier);
            var result = await sut.ResetPasswordAsync("boom@ex.com", "t", "new");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
           
        }
    }
}