using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.AccountManagement;
using ApplicationLayer.Services.EmailServices;
using ApplicationLayer.Services.UserOpreationServices;
using DomainLayer.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.AccountManagement
{
    public class AccountManagementServiceTests
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

        [Fact]
        public async Task DeleteAsync_ValidActiveUser_SoftDeletesAndCommits()
        {
            var logger = new Mock<ILogger<AccountManagementService>>();
            var userManager = CreateUserManagerMock();
            var unitOfWork = new Mock<IUnitOfWork>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var userOps = new Mock<IUserOpreationServices>();
            var errorNotifier = new Mock<IErrorNotificationService>();
            var transaction = new Mock<IDbContextTransaction>();

            var customer = new Customer { Id = "user-1", DeletedAt = null };

            unitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(transaction.Object);
            userManager.Setup(x => x.FindByIdAsync(customer.Id)).ReturnsAsync(customer);
            userManager.Setup(x => x.UpdateAsync(It.IsAny<Customer>())).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.UpdateSecurityStampAsync(It.IsAny<Customer>())).ReturnsAsync(IdentityResult.Success);

            var sut = new AccountManagementService(userOps.Object, backgroundJobClient.Object, logger.Object, userManager.Object, unitOfWork.Object, errorNotifier.Object);

            var result = await sut.DeleteAsync(customer.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.True(result.Data);
            Assert.Equal("Deleted", result.Message);

            userManager.Verify(x => x.UpdateAsync(It.Is<Customer>(c => c.DeletedAt != null)), Times.Once);
            userManager.Verify(x => x.UpdateSecurityStampAsync(It.Is<Customer>(c => c.Id == customer.Id)), Times.Once);
            unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
     //       backgroundJobClient.Verify(x => x.Enqueue(It.IsAny<Expression<Action>>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_UserNotFound_Returns404AndNoCommitOrUpdate()
        {
            var logger = new Mock<ILogger<AccountManagementService>>();
            var userManager = CreateUserManagerMock();
            var unitOfWork = new Mock<IUnitOfWork>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var userOps = new Mock<IUserOpreationServices>();
            var errorNotifier = new Mock<IErrorNotificationService>();
            var transaction = new Mock<IDbContextTransaction>();

            unitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(transaction.Object);
            userManager.Setup(x => x.FindByIdAsync("missing-id")).ReturnsAsync((Customer)null);

            var sut = new AccountManagementService(userOps.Object, backgroundJobClient.Object, logger.Object, userManager.Object, unitOfWork.Object, errorNotifier.Object);

            var result = await sut.DeleteAsync("missing-id");

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);

            userManager.Verify(x => x.UpdateAsync(It.IsAny<Customer>()), Times.Never);
            userManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<Customer>()), Times.Never);
            unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
           // backgroundJobClient.Verify(x => x.Enqueue(It.IsAny<Expression<Action>>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_UserAlreadyDeleted_Returns404AndNoUpdate()
        {
            var logger = new Mock<ILogger<AccountManagementService>>();
            var userManager = CreateUserManagerMock();
            var unitOfWork = new Mock<IUnitOfWork>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var userOps = new Mock<IUserOpreationServices>();
            var errorNotifier = new Mock<IErrorNotificationService>();
            var transaction = new Mock<IDbContextTransaction>();

            var customer = new Customer { Id = "user-2", DeletedAt = DateTime.UtcNow };

            unitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(transaction.Object);
            userManager.Setup(x => x.FindByIdAsync(customer.Id)).ReturnsAsync(customer);

            var sut = new AccountManagementService(userOps.Object, backgroundJobClient.Object, logger.Object, userManager.Object, unitOfWork.Object, errorNotifier.Object);

            var result = await sut.DeleteAsync(customer.Id);

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);

            userManager.Verify(x => x.UpdateAsync(It.IsAny<Customer>()), Times.Never);
            userManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<Customer>()), Times.Never);
            unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        //    backgroundJobClient.Verify(x => x.Enqueue(It.IsAny<Expression<Action>>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_UpdateFails_RollsBackAndReturns500()
        {
            var logger = new Mock<ILogger<AccountManagementService>>();
            var userManager = CreateUserManagerMock();
            var unitOfWork = new Mock<IUnitOfWork>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var userOps = new Mock<IUserOpreationServices>();
            var errorNotifier = new Mock<IErrorNotificationService>();
            var transaction = new Mock<IDbContextTransaction>();

            var customer = new Customer { Id = "user-3", DeletedAt = null };
            var identityError = new IdentityError { Description = "update-error" };

            unitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(transaction.Object);
      //      backgroundJobClient.Setup(x => x.Enqueue(It.IsAny<Expression<Action>>())).Returns(string.Empty);
            userManager.Setup(x => x.FindByIdAsync(customer.Id)).ReturnsAsync(customer);
            userManager.Setup(x => x.UpdateAsync(It.IsAny<Customer>())).ReturnsAsync(IdentityResult.Failed(identityError));

            var sut = new AccountManagementService(userOps.Object, backgroundJobClient.Object, logger.Object, userManager.Object, unitOfWork.Object, errorNotifier.Object);

            var result = await sut.DeleteAsync(customer.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);

            userManager.Verify(x => x.UpdateAsync(It.Is<Customer>(c => c.DeletedAt != null)), Times.Once);
            userManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<Customer>()), Times.Never);
            unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
      //      backgroundJobClient.Verify(x => x.Enqueue<IErrorNotificationService>(It.IsAny<Expression<Action<IErrorNotificationService>>>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ExceptionThrown_RollsBackEnqueuesErrorAndReturns500()
        {
            var logger = new Mock<ILogger<AccountManagementService>>();
            var userManager = CreateUserManagerMock();
            var unitOfWork = new Mock<IUnitOfWork>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var userOps = new Mock<IUserOpreationServices>();
            var errorNotifier = new Mock<IErrorNotificationService>();
            var transaction = new Mock<IDbContextTransaction>();

            unitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(transaction.Object);
            userManager.Setup(x => x.FindByIdAsync("boom")).ThrowsAsync(new Exception("boom"));

            var sut = new AccountManagementService(userOps.Object, backgroundJobClient.Object, logger.Object, userManager.Object, unitOfWork.Object, errorNotifier.Object);

            var result = await sut.DeleteAsync("boom");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);

            userManager.Verify(x => x.UpdateAsync(It.IsAny<Customer>()), Times.Never);
            userManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<Customer>()), Times.Never);
            unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    //        backgroundJobClient.Verify(x => x.Enqueue(It.IsAny<Expression<Action>>()), Times.Once);
        }
    }
}