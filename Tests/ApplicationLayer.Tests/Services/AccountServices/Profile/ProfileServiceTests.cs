using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.CustomerAddressDtos;
using ApplicationLayer.DtoModels.AccountDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AccountServices.Profile;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Enums;
using DomainLayer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ApplicationLayer.DtoModels.CustomerAddressDtos;

namespace ApplicationLayer.Tests.Services.AccountServices.Profile
{
    public class ProfileServiceTests
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

        private static ProfileService CreateSut(
            Mock<ICustomerAddressServices> addr,
            Mock<IHttpContextAccessor> http,
            Mock<ILogger<ProfileService>> logger,
            Mock<UserManager<Customer>> userManager,
            Mock<IImagesServices> images,
            Mock<IUnitOfWork> uow,
            Mock<IErrorNotificationService> errors)
        {
            return new ProfileService(
                addr.Object,
                http.Object,
                logger.Object,
                userManager.Object,
                images.Object,
                uow.Object,
                errors.Object
            );
        }

        [Fact]
        public async Task ChangeEmailAsync_UserNotFound_Returns404_NoTransaction()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((Customer)null);

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.ChangeEmailAsync("new@ex.com", "missing");

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
            userManager.Verify(x => x.SetEmailAsync(It.IsAny<Customer>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeEmailAsync_EmptyEmail_Returns400()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(new Customer { Id = "u1", Email = "old@ex.com" });

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.ChangeEmailAsync(" ", "u1");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task ChangeEmailAsync_SameEmail_Returns400()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer { Id = "u2", Email = "same@ex.com" };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.ChangeEmailAsync("same@ex.com", user.Id);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
            userManager.Verify(x => x.SetEmailAsync(It.IsAny<Customer>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeEmailAsync_DuplicateEmail_Returns409_BeginsNoCommit()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var user = new Customer { Id = "u3", Email = "old@ex.com" };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync("dup@ex.com")).ReturnsAsync(new Customer { Id = "other" });

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.ChangeEmailAsync("dup@ex.com", user.Id);

            Assert.False(result.Success);
            Assert.Equal(409, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Once);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ChangeEmailAsync_UpdateFails_Returns400_NoCommit()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var user = new Customer { Id = "u4", Email = "old@ex.com" };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync("new@ex.com")).ReturnsAsync((Customer)null);
            userManager.Setup(x => x.SetEmailAsync(user, "new@ex.com")).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "bad" }));

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.ChangeEmailAsync("new@ex.com", user.Id);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ChangeEmailAsync_Success_CommitsAndReturnsDto()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var user = new Customer { Id = "u5", Email = "old@ex.com" };
            userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByEmailAsync("new@ex.com")).ReturnsAsync((Customer)null);
            userManager.Setup(x => x.SetEmailAsync(user, "new@ex.com")).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("tok");
            http.Setup(x => x.HttpContext).Returns((HttpContext)null);

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.ChangeEmailAsync("new@ex.com", user.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("new@ex.com", result.Data.NewEmail);
            Assert.Equal("please go to email confirm", result.Data.Note);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UploadPhotoAsync_NoImage_Returns400()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.UploadPhotoAsync(null, "u1");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task UploadPhotoAsync_SaveFails_Returns500_NoTransaction()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.Length).Returns(10);
            images.Setup(x => x.SaveCustomerImageAsync(file.Object, "u2")).ReturnsAsync(Result<Image>.Fail("err"));

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.UploadPhotoAsync(file.Object, "u2");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            uow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task UploadPhotoAsync_UserNotFound_Returns401_NoCommit()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.Length).Returns(10);
            var saved = new Image { Id = 1, Url = "http://img" };
            images.Setup(x => x.SaveCustomerImageAsync(file.Object, "u3")).ReturnsAsync(Result<Image>.Ok(saved));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userManager.Setup(x => x.FindByIdAsync("u3")).ReturnsAsync((Customer)null);

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.UploadPhotoAsync(file.Object, "u3");

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadPhotoAsync_UpdateFails_RollsBackAndReturns500()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.Length).Returns(10);
            var saved = new Image { Id = 2, Url = "http://img2" };
            images.Setup(x => x.SaveCustomerImageAsync(file.Object, "u4")).ReturnsAsync(Result<Image>.Ok(saved));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            var repo = new Mock<IRepository<UserOperationsLog>>();
            repo.Setup(r => r.CreateAsync(It.IsAny<UserOperationsLog>())).ReturnsAsync(new UserOperationsLog { Id = 1 });
            uow.Setup(x => x.Repository<UserOperationsLog>()).Returns(repo.Object);

            var customer = new Customer { Id = "u4", DeletedAt = null };
            userManager.Setup(x => x.FindByIdAsync(customer.Id)).ReturnsAsync(customer);
            userManager.Setup(x => x.UpdateAsync(customer)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "bad" }));

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.UploadPhotoAsync(file.Object, customer.Id);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UploadPhotoAsync_Success_CommitsReturns200()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();
            var tx = new Mock<IDbContextTransaction>();

            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.Length).Returns(10);
            var saved = new Image { Id = 3, Url = "http://ok" };
            images.Setup(x => x.SaveCustomerImageAsync(file.Object, "u5")).ReturnsAsync(Result<Image>.Ok(saved));
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var customer = new Customer { Id = "u5", DeletedAt = null };
            userManager.Setup(x => x.FindByIdAsync(customer.Id)).ReturnsAsync(customer);
            userManager.Setup(x => x.UpdateAsync(customer)).ReturnsAsync(IdentityResult.Success);
            var repo = new Mock<IRepository<UserOperationsLog>>();
            repo.Setup(r => r.CreateAsync(It.IsAny<UserOperationsLog>())).ReturnsAsync(new UserOperationsLog { Id = 1 });
            uow.Setup(u=>u.Repository<UserOperationsLog>()).Returns(repo.Object);
            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.UploadPhotoAsync(file.Object, customer.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("http://ok", result.Data.ImageUrl);
            tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReplaceCustomerImageAsync_WithExistingImage_DeletesNewImageAndCommits()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var repo = new Mock<IRepository<UserOperationsLog>>();
            repo.Setup(r => r.CreateAsync(It.IsAny<UserOperationsLog>())).ReturnsAsync(new UserOperationsLog { Id = 1 });
            uow.Setup(x => x.Repository<UserOperationsLog>()).Returns(repo.Object);

            var customer = new Customer { Id = "u6", Image = new Image { Id = 10, Url = "http://old" } };
            var newImage = new Image { Id = 11, Url = "http://new" };

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            await sut.ReplaceCustomerImageAsync(customer, newImage);

            images.Verify(x => x.DeleteImageAsync(newImage), Times.Once);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            Assert.Equal("http://new", customer.Image.Url);
        }

        [Fact]
        public async Task GetProfileAsync_UserNotFound_Returns404()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((Customer)null);

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.GetProfileAsync("missing");

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task GetProfileAsync_Success_ReturnsDtoWithAddresses()
        {
            var addr = new Mock<ICustomerAddressServices>();
            var http = new Mock<IHttpContextAccessor>();
            var logger = new Mock<ILogger<ProfileService>>();
            var userManager = CreateUserManagerMock();
            var images = new Mock<IImagesServices>();
            var uow = new Mock<IUnitOfWork>();
            var errors = new Mock<IErrorNotificationService>();

            var user = new Customer
            {
                Id = "u7",
                Name = "N",
                Email = "e@x.com",
                PhoneNumber = "123",
                EmailConfirmed = true,
                Gender = Gender.Man,
                UserName = "u",
                Image = new Image { Id = 1, Url = "http://img" }
            };
			userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
			addr.Setup(i => i.GetCustomerAddressesAsync(user.Id)).ReturnsAsync(Result<List<CustomerAddressDto>>.Ok(new List<CustomerAddressDto>()));

            var sut = CreateSut(addr, http, logger, userManager, images, uow, errors);
            var result = await sut.GetProfileAsync(user.Id);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("e@x.com", result.Data.Email);
            Assert.Equal("http://img", result.Data.ProfileImage.Url);
        }
    }
}