using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.UserMangment;
using ApplicationLayer.Services.AdminOperationServices;
using DomainLayer.Enums;
using DomainLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.UserMangment
{
    public class UserRoleMangementServiceTests
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

        private static Mock<RoleManager<IdentityRole>> CreateRoleManagerMock()
        {
            var store = new Mock<IRoleStore<IdentityRole>>();
            var roleValidators = Array.Empty<IRoleValidator<IdentityRole>>();
            var keyNormalizer = new Mock<ILookupNormalizer>();
            var errors = new IdentityErrorDescriber();
            var logger = new Mock<ILogger<RoleManager<IdentityRole>>>();

            return new Mock<RoleManager<IdentityRole>>(
                store.Object,
                roleValidators,
                keyNormalizer.Object,
                errors,
                logger.Object
            );
        }

        private static UserRoleMangementService CreateSut(
            Mock<IUnitOfWork> uow,
            Mock<IAdminOpreationServices> adminOps,
            Mock<UserManager<Customer>> userManager,
            Mock<RoleManager<IdentityRole>> roleManager,
            Mock<ILogger<UserRoleMangementService>> logger)
        {
            return new UserRoleMangementService(
                uow.Object,
                adminOps.Object,
                userManager.Object,
                roleManager.Object,
                logger.Object
            );
        }

        //[Fact]
        //public async Task GetAllRolesAsync_NoRoles_Returns404()
        //{
        //    var uow = new Mock<IUnitOfWork>();
        //    var adminOps = new Mock<IAdminOpreationServices>();
        //    var userManager = CreateUserManagerMock();
        //    var roleManager = CreateRoleManagerMock();
        //    var logger = new Mock<ILogger<UserRoleMangementService>>();

        //    var rolesQueryable = new List<IdentityRole>().AsQueryable();
        //    roleManager.SetupGet(r => r.Roles).Returns(rolesQueryable.Object);

        //    var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
        //    var result = await sut.GetAllRolesAsync();

        //    Assert.False(result.Success);
        //    Assert.Equal(404, result.StatusCode);
        //}

        //[Fact]
        //public async Task GetAllRolesAsync_ReturnsRoleNames()
        //{
        //    var uow = new Mock<IUnitOfWork>();
        //    var adminOps = new Mock<IAdminOpreationServices>();
        //    var userManager = CreateUserManagerMock();
        //    var roleManager = CreateRoleManagerMock();
        //    var logger = new Mock<ILogger<UserRoleMangementService>>();

        //    var roles = new List<IdentityRole> { new IdentityRole("User"), new IdentityRole("Admin") };
        //    var rolesQueryable = roles.AsQueryable().BuildMock();
        //    roleManager.SetupGet(r => r.Roles).Returns(rolesQueryable.Object);

        //    var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
        //    var result = await sut.GetAllRolesAsync();

        //    Assert.True(result.Success);
        //    Assert.Contains("User", result.Data);
        //    Assert.Contains("Admin", result.Data);
        //}

        [Fact]
        public async Task AddRoleToUserAsync_InvalidInputs_Returns400()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync(" ", "", "admin");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task AddRoleToUserAsync_AdminSameAsUser_Returns403()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync("id", "Role", "id");

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task AddRoleToUserAsync_AdminNotSuperAdmin_Returns403()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();

            var admin = new Customer { Id = "admin" };
            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(false);

            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync("user", "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task AddRoleToUserAsync_RoleNotExists_Returns400()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();

            var admin = new Customer { Id = "admin" };
            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(false);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync("user", "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task AddRoleToUserAsync_UserNotFound_Returns404()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();

            var admin = new Customer { Id = "admin" };
            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync("user")).ReturnsAsync((Customer)null);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync("user", "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task AddRoleToUserAsync_AddFails_RollsBack400()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.AddToRoleAsync(user, "Role")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "err" }));

            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync(user.Id, "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            adminOps.Verify(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<List<int>>()), Times.Never);
        }

        [Fact]
        public async Task AddRoleToUserAsync_LogFails_RollsBack500()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.AddToRoleAsync(user, "Role")).ReturnsAsync(IdentityResult.Success);
            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Fail("log err"));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync(user.Id, "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddRoleToUserAsync_Success_Commits200()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.AddToRoleAsync(user, "Role")).ReturnsAsync(IdentityResult.Success);
            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Ok(new AdminOperationsLog()));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.AddRoleToUserAsync(user.Id, "Role", admin.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveRoleFromUserAsync_NotAssigned_Returns400()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.IsInRoleAsync(user, "Role")).ReturnsAsync(false);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.RemoveRoleFromUserAsync(user.Id, "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task RemoveRoleFromUserAsync_RemoveFails_RollsBack400()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.IsInRoleAsync(user, "Role")).ReturnsAsync(true);
            userManager.Setup(x => x.RemoveFromRoleAsync(user, "Role")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "err" }));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.RemoveRoleFromUserAsync(user.Id, "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveRoleFromUserAsync_LogFails_RollsBack500()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.IsInRoleAsync(user, "Role")).ReturnsAsync(true);
            userManager.Setup(x => x.RemoveFromRoleAsync(user, "Role")).ReturnsAsync(IdentityResult.Success);
            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Fail("log err"));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.RemoveRoleFromUserAsync(user.Id, "Role", admin.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveRoleFromUserAsync_Success_Commits200()
        {
            var uow = new Mock<IUnitOfWork>();
            var adminOps = new Mock<IAdminOpreationServices>();
            var userManager = CreateUserManagerMock();
            var roleManager = CreateRoleManagerMock();
            var logger = new Mock<ILogger<UserRoleMangementService>>();
            var tx = new Mock<IDbContextTransaction>();

            var admin = new Customer { Id = "admin" };
            var user = new Customer { Id = "user" };

            userManager.Setup(x => x.FindByIdAsync(admin.Id)).ReturnsAsync(admin);
            userManager.Setup(x => x.IsInRoleAsync(admin, "SuperAdmin")).ReturnsAsync(true);
            roleManager.Setup(x => x.RoleExistsAsync("Role")).ReturnsAsync(true);
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.IsInRoleAsync(user, "Role")).ReturnsAsync(true);
            userManager.Setup(x => x.RemoveFromRoleAsync(user, "Role")).ReturnsAsync(IdentityResult.Success);
            adminOps.Setup(x => x.AddAdminOpreationAsync(It.IsAny<string>(), It.IsAny<Opreations>(), user.Id, It.IsAny<List<int>>()))
                .ReturnsAsync(Result<AdminOperationsLog>.Ok(new AdminOperationsLog()));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(uow, adminOps, userManager, roleManager, logger);
            var result = await sut.RemoveRoleFromUserAsync(user.Id, "Role", admin.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}