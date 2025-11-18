using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.UserMangment;
using ApplicationLayer.Services.AdminOperationServices;
using DomainLayer.Enums;
using DomainLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.UserMangment
{
    public class UserAccountManagementServiceTests
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

        private static UserAccountManagementService CreateSut(
            Mock<IUnitOfWork> uow,
            Mock<IAdminOpreationServices> adminOps,
            Mock<UserManager<Customer>> userManager,
            Mock<ILogger<UserAccountManagementService>> logger)
        {
            return new UserAccountManagementService(
                uow.Object,
                adminOps.Object,
                userManager.Object,
                logger.Object
            );
        }

        [Fact]
        public async Task LockUserAsync_InvalidUserId_Returns400()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync(" ", "admin-1");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task LockUserAsync_AdminSameAsUser_Returns403()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync("id-1", "id-1");

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task LockUserAsync_AdminNotSuperAdmin_Returns403()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();

            var admin = new Customer { Id = "admin-1" };
            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(false);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync("user-1", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task LockUserAsync_UserNotFound_Returns404()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();

            var admin = new Customer { Id = "admin-1" };
            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync((Customer)null);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync("user-1", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task LockUserAsync_UserOperationFails_RollsBack500()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin-1" };
            var user = new Customer { Id = "user-1" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

            userManager.Setup(x => x.SetLockoutEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>())).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "fail" }));

            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync(user.Id, admin.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            adminOps.Verify(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), It.IsAny<string>(), It.IsAny<System.Collections.Generic.List<int>>()), Times.Never);
            uow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task LockUserAsync_AdminLogFails_RollsBack500()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin-1" };
            var user = new Customer { Id = "user-1" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.SetLockoutEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>())).ReturnsAsync(IdentityResult.Success);

            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<System.Collections.Generic.List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Fail("log fail"));

            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync(user.Id, admin.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            uow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task LockUserAsync_Success_Commits200()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin-1" };
            var user = new Customer { Id = "user-1" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.SetLockoutEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>())).ReturnsAsync(IdentityResult.Success);

            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<System.Collections.Generic.List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Ok(new AdminOperationsLog()))
                ;

            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.LockUserAsync(user.Id, admin.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnlockUserAsync_Success_Commits200()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin-1" };
            var user = new Customer { Id = "user-1" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.SetLockoutEndDateAsync(user, null)).ReturnsAsync(IdentityResult.Success);
            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<System.Collections.Generic.List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Ok(new AdminOperationsLog()));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.UnlockUserAsync(user.Id, admin.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_UpdateFails_RollsBack500()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin-1" };
            var user = new Customer { Id = "user-1" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "bad" }));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.DeleteUserAsync(user.Id, admin.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            adminOps.Verify(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<System.Collections.Generic.List<int>>()), Times.Never);
        }

        [Fact]
        public async Task RestoreUserAsync_Success_Commits200()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var logger = new Mock<ILogger<UserAccountManagementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin-1" };
            var user = new Customer { Id = "user-1", DeletedAt = DateTime.UtcNow };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<System.Collections.Generic.List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Ok(new AdminOperationsLog()));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, logger);
            var result = await sut.RestoreUserAsync(user.Id, admin.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}