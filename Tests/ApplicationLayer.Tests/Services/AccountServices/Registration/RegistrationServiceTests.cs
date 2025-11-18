using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DtoModels.AccountDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.Registration;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Enums;
using DomainLayer.Models;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.Registration
{
    public class RegistrationServiceTests
    {
        static RegistrationServiceTests()
        {
            GlobalConfiguration.Configuration.UseMemoryStorage();
        }

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

        private static RegistrationService CreateSut(
            Mock<ICustomerFactory> factory,
            Mock<IHttpContextAccessor> http,
            Mock<ILogger<RegistrationService>> logger,
            Mock<UserManager<Customer>> userManager,
            Mock<IUnitOfWork> uow,
            Mock<IErrorNotificationService> errors)
        {
            return new RegistrationService(
                factory.Object,
                http.Object,
                logger.Object,
                userManager.Object,
                uow.Object,
                errors.Object
            );
        }

        [Fact]
        public async Task RegisterAsync_EmailExists_Returns409_NoCreateOrCommit()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var dto = new RegisterDto { Email = "dup@ex.com", UserName = "u", Name = "n", PhoneNumber = "123", Gender = Gender.Man, Age = 20, Password = "P@ssw0rd" };
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync(dto.Email)).ReturnsAsync(new Customer());

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(409, result.StatusCode);
            userManager.Verify(x => x.CreateAsync(It.IsAny<Customer>(), It.IsAny<string>()), Times.Never);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_CreateFails_Returns400_NoAddRoleOrCommit()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var dto = new RegisterDto { Email = "e@x.com", UserName = "u", Name = "n", PhoneNumber = "123", Gender = Gender.Woman, Age = 22, Password = "P@ssw0rd" };
            var customer = new Customer { Email = dto.Email, UserName = dto.UserName };
            factory.Setup(x => x.CreateCustomer(dto)).Returns(customer);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync(dto.Email)).ReturnsAsync((Customer)null);
            userManager.Setup(x => x.CreateAsync(customer, dto.Password)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "bad" }));

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            userManager.Verify(x => x.AddToRoleAsync(It.IsAny<Customer>(), It.IsAny<string>()), Times.Never);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_AddRoleFails_RollsBackDeletesAndReturns500()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var dto = new RegisterDto { Email = "e2@x.com", UserName = "u2", Name = "n2", PhoneNumber = "123", Gender = Gender.Kids, Age = 18, Password = "P@ssw0rd" };
            var customer = new Customer { Id = "id-1", Email = dto.Email, UserName = dto.UserName };
            factory.Setup(x => x.CreateCustomer(dto)).Returns(customer);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync(dto.Email)).ReturnsAsync((Customer)null);
            userManager.Setup(x => x.CreateAsync(customer, dto.Password)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.AddToRoleAsync(customer, "User")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "role fail" }));
            userManager.Setup(x => x.DeleteAsync(customer)).ReturnsAsync(IdentityResult.Success);

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            userManager.Verify(x => x.DeleteAsync(customer), Times.Once);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_Success_CommitsReturns201()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var dto = new RegisterDto { Email = "e3@x.com", UserName = "u3", Name = "n3", PhoneNumber = "123", Gender = Gender.Uni, Age = 30, Password = "P@ssw0rd" };
            var customer = new Customer { Id = "id-2", Email = dto.Email, UserName = dto.UserName, Name = dto.Name, PhoneNumber = dto.PhoneNumber, Age = dto.Age };
            factory.Setup(x => x.CreateCustomer(dto)).Returns(customer);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync(dto.Email)).ReturnsAsync((Customer)null);
            userManager.Setup(x => x.CreateAsync(customer, dto.Password)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.AddToRoleAsync(customer, "User")).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(customer)).ReturnsAsync("tok");

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.RegisterAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal(dto.Email, result.Data.Email);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ExceptionThrown_RollsBackReturns500()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var dto = new RegisterDto { Email = "boom@ex.com", UserName = "u4", Name = "n4", PhoneNumber = "123", Gender = Gender.Man, Age = 19, Password = "P@ssw0rd" };
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync(dto.Email)).ThrowsAsync(new Exception("boom"));

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmEmailAsync_UserNotFound_ReturnsOk200()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((Customer)null);

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ConfirmEmailAsync("missing", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("tok")));

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task ConfirmEmailAsync_AlreadyConfirmed_Returns400()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u1", EmailConfirmed = true };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ConfirmEmailAsync(user.Id, Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("tok")));

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task ConfirmEmailAsync_InvalidToken_Returns400()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u2", EmailConfirmed = false };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.ConfirmEmailAsync(user, "bad")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "bad" }));

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ConfirmEmailAsync(user.Id, Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("bad")));

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task ConfirmEmailAsync_Success_Returns200()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u3", EmailConfirmed = false };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(x => x.ConfirmEmailAsync(user, "tok")).ReturnsAsync(IdentityResult.Success);

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ConfirmEmailAsync(user.Id, Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("tok")));

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task ResendConfirmationEmailAsync_UserNotFound_ReturnsOk200()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByEmailAsync("missing@ex.com")).ReturnsAsync((Customer)null);

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ResendConfirmationEmailAsync("missing@ex.com");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task ResendConfirmationEmailAsync_AlreadyConfirmed_Returns400()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u8", Email = "e@x.com", EmailConfirmed = true };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ResendConfirmationEmailAsync(user.Email);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task ResendConfirmationEmailAsync_Success_Returns200()
        {
            var factory = new Mock<ICustomerFactory>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<RegistrationService>>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u9", Email = "e9@x.com", EmailConfirmed = false };
            userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("tok");

            var sut = CreateSut(factory, http, logger, userManager, uow, errors);
            var result = await sut.ResendConfirmationEmailAsync(user.Email);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }
    }
}