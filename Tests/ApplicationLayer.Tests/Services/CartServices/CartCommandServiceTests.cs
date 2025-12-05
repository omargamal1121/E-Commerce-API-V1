using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DtoModels.CartDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.CartServices;
using ApplicationLayer.Services.UserOpreationServices;
using DomainLayer.Enums;
using DomainLayer.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.CartServices
{
    public class CartCommandServiceTests
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

        private static CartCommandService CreateSut(
            Mock<ILogger<CartCommandService>> logger,
            Mock<IBackgroundJobClient> bg,
            Mock<UserManager<Customer>> userManager,
            Mock<IUnitOfWork> uow,
            Mock<ICartRepository> cartRepo,
            Mock<IUserOpreationServices> userOps,
            Mock<ICartCacheHelper> cache)
        {
            uow.SetupGet(x => x.Cart).Returns(cartRepo.Object);
            return new CartCommandService(
                logger.Object,
                bg.Object,
                userManager.Object,
                uow.Object,
                cartRepo.Object,
                userOps.Object,
                cache.Object
            );
        }

        [Fact]
        public async Task AddItemToCartAsync_CustomerMissing_Returns404()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();

            userManager.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync((Customer)null);
            var tx = new Mock<IDbContextTransaction>();
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.AddItemToCartAsync("u1", new CreateCartItemDto { ProductId = 10, ProductVariantId = 100, Quantity = 1 });

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCartItemAsync_InvalidInputs_Returns400()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);

            var r1 = await sut.UpdateCartItemAsync(" ", 1, new UpdateCartItemDto { Quantity = 1 }, 2);
            Assert.False(r1.Success);
            Assert.Equal(400, r1.StatusCode);

            var r2 = await sut.UpdateCartItemAsync("u1", 1, null, 2);
            Assert.False(r2.Success);
            Assert.Equal(400, r2.StatusCode);

            var r3 = await sut.UpdateCartItemAsync("u1", 1, new UpdateCartItemDto { Quantity = 0 }, 2);
            Assert.False(r3.Success);
            Assert.Equal(400, r3.StatusCode);

            var r4 = await sut.UpdateCartItemAsync("u1", 1, new UpdateCartItemDto { Quantity = 1 }, null);
            Assert.False(r4.Success);
            Assert.Equal(400, r4.StatusCode);
        }

        [Fact]
        public async Task UpdateCartItemAsync_CartNotFound_Returns404()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync((Cart)null);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.UpdateCartItemAsync("u1", 10, new UpdateCartItemDto { Quantity = 2 }, 100);

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCartItemAsync_ItemNotFound_Returns404()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            var cart = new Cart { Id = 1, UserId = "u1", Items = new List<CartItem>() };
            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync(cart);
            cartRepo.Setup(x => x.LockCartForUpdateAsnyc(cart.Id)).Returns(Task.CompletedTask);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.UpdateCartItemAsync("u1", 10, new UpdateCartItemDto { Quantity = 2 }, 100);

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCartItemAsync_QuantityUnchanged_ReturnsOk_NoCommit()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            var cart = new Cart
            {
                Id = 2,
                UserId = "u1",
                Items = new List<CartItem>
                {
                    new CartItem { ProductId = 10, ProductVariantId = 100, Quantity = 3 }
                }
            };

            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync(cart);
            cartRepo.Setup(x => x.LockCartForUpdateAsnyc(cart.Id)).Returns(Task.CompletedTask);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.UpdateCartItemAsync("u1", 10, new UpdateCartItemDto { Quantity = 3 }, 100);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_Success_CommitsAndClearsCache()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            var cart = new Cart { Id = 3, UserId = "u1", Items = new List<CartItem>() };

            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync(cart);
            cartRepo.Setup(x => x.LockCartForUpdateAsnyc(cart.Id)).Returns(Task.CompletedTask);
            cartRepo.Setup(x => x.RemoveCartItemAsync(cart.Id, 10, 100)).ReturnsAsync(true);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userOps.Setup(x => x.AddUserOpreationAsync(It.IsAny<string>(), Opreations.UpdateOpreation, "u1", cart.Id))
                   .ReturnsAsync(Result<UserOperationsLog>.Ok(new UserOperationsLog { Id = 1 }));

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.RemoveItemFromCartAsync("u1", new RemoveCartItemDto { ProductId = 10, ProductVariantId = 100 });

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            cache.Verify(x => x.RemoveCartCacheAsync("u1"), Times.Once);
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_AdminLogFails_Returns500()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            var cart = new Cart { Id = 4, UserId = "u1", Items = new List<CartItem>() };
            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync(cart);
            cartRepo.Setup(x => x.LockCartForUpdateAsnyc(cart.Id)).Returns(Task.CompletedTask);
            cartRepo.Setup(x => x.RemoveCartItemAsync(cart.Id, 10, 100)).ReturnsAsync(true);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userOps.Setup(x => x.AddUserOpreationAsync(It.IsAny<string>(), Opreations.UpdateOpreation, "u1", cart.Id))
                   .ReturnsAsync(Result<UserOperationsLog>.Fail("err"));

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.RemoveItemFromCartAsync("u1", new RemoveCartItemDto { ProductId = 10, ProductVariantId = 100 });

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task ClearCartAsync_Success_Commits()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            var cart = new Cart { Id = 5, UserId = "u1" };
            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync(cart);
            cartRepo.Setup(x => x.LockCartForUpdateAsnyc(cart.Id)).Returns(Task.CompletedTask);
            cartRepo.Setup(x => x.ClearCartAsync(cart.Id)).ReturnsAsync(true);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            userOps.Setup(x => x.AddUserOpreationAsync(It.IsAny<string>(), Opreations.UpdateOpreation, "u1", cart.Id))
                   .ReturnsAsync(Result<UserOperationsLog>.Ok(new UserOperationsLog { Id = 1 }));

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.ClearCartAsync("u1");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            uow.Verify(x => x.CommitAsync(), Times.Once);
            cache.Verify(x => x.RemoveCartCacheAsync("u1"), Times.Once);
        }

        [Fact]
        public async Task ClearCartAsync_CartMissing_Returns404()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();
            var tx = new Mock<IDbContextTransaction>();

            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync((Cart)null);
            uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.ClearCartAsync("u1");

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCheckoutData_NoCart_CreateFails_ReturnsFail()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();

            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync((Cart)null);
            userManager.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync((Customer)null);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.UpdateCheckoutData("u1");

            Assert.False(result.Success);
            Assert.Equal("Unexpected error while creating a new cart", result.Message);
        }

        [Fact]
        public async Task UpdateCheckoutData_CartExists_SetsCheckoutAndCommits()
        {
            var logger = new Mock<ILogger<CartCommandService>>();
            var bg = new Mock<IBackgroundJobClient>();
            var userManager = CreateUserManagerMock();
            var uow = new Mock<IUnitOfWork>();
            var cartRepo = new Mock<ICartRepository>();
            var userOps = new Mock<IUserOpreationServices>();
            var cache = new Mock<ICartCacheHelper>();

            var cart = new Cart { Id = 7, UserId = "u1" };
            cartRepo.Setup(x => x.GetCartByUserIdAsync("u1")).ReturnsAsync(cart);
            uow.Setup(x => x.CommitAsync()).ReturnsAsync(1);

            var sut = CreateSut(logger, bg, userManager, uow, cartRepo, userOps, cache);
            var result = await sut.UpdateCheckoutData("u1");

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.NotNull(cart.CheckoutDate);
            cache.Verify(x => x.RemoveCartCacheAsync("u1"), Times.Once);
        }
    }
}