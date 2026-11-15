using Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DtoModels.AccountDtos;
using Application.DtoModels.CustomerAddressDtos;
using Application.Services.AccountServices.UserMangment;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Application.Tests.Services.AccountServices.UserMangment
{
    public class UserMangerMappingTests
    {
        private static Mock<UserManager<Customer>> CreateUserManagerMock()
        {
            var store = new Mock<IUserStore<Customer>>();
            var options = new Mock<IOptions<IdentityOptions>>();
            options.Setup(o => o.Value).Returns(new IdentityOptions());
            var passwordHasher = new Mock<IPasswordHasher<Customer>>();
            var userValidators = Array.Empty<IUserValidator<Customer>>();
            var passwordValidators = Array.Empty<IPasswordValidator<Customer>>();
            var keyNormalizer = new Mock<ILookupNormalizer>();
            var errors = new IdentityErrorDescriber();
            var services = new Mock<IServiceProvider>();
            var logger = new Mock<ILogger<UserManager<Customer>>>();

            var mock = new Mock<UserManager<Customer>>(
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

            mock.Setup(m => m.GetRolesAsync(It.IsAny<Customer>()))
                .ReturnsAsync(new List<string> { "User" });

            return mock;
        }

        private static UserMangerMapping CreateSut(Mock<UserManager<Customer>>? userManager = null)
        {
            var uow = new Mock<IUnitOfWork>();
            var mgr = userManager ?? CreateUserManagerMock();
            return new UserMangerMapping(uow.Object, mgr.Object);
        }

        [Fact]
        public async Task ToUserDto_SingleCustomerNoLockout_IsActiveTrueAndNotLocked()
        {
            var customer = new Customer
            {
                Id = "u1",
                Name = "Alice",
                Email = "alice@example.com",
                UserName = "alice",
                PhoneNumber = "1234567890",
                LockoutEnd = null,
                DeletedAt = null
            };

            var sut = CreateSut();
            var dto = await sut.ToUserDto(customer);

            Assert.Equal("u1", dto.Id);
            Assert.True(dto.IsActive);
            Assert.False(dto.IsLock);
            Assert.False(dto.IsDeleted);
        }

        [Fact]
        public async Task ToUserDto_SingleCustomerLockedInFuture_IsActiveFalseAndIsLockTrue()
        {
            var customer = new Customer
            {
                Id = "u2",
                Name = "Bob",
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(10),
                DeletedAt = null
            };

            var sut = CreateSut();
            var dto = await sut.ToUserDto(customer);

            Assert.False(dto.IsActive);
            Assert.True(dto.IsLock);
            Assert.False(dto.IsDeleted);
        }

        [Fact]
        public async Task ToUserDto_SingleCustomerLockoutExpired_IsActiveTrueAndIsLockFalse()
        {
            var customer = new Customer
            {
                Id = "u3",
                Name = "Charlie",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-10),
                DeletedAt = null
            };

            var sut = CreateSut();
            var dto = await sut.ToUserDto(customer);

            Assert.True(dto.IsActive);
            Assert.False(dto.IsLock);
            Assert.False(dto.IsDeleted);
        }

        [Fact]
        public async Task ToUserDto_SingleCustomerWithAddresses_MapsAllFieldsAndAddresses()
        {
            var customer = new Customer
            {
                Id = "u4",
                Name = "Dave",
                Email = "d@x.com",
                UserName = "dave",
                PhoneNumber = "444",
                LockoutEnd = DateTimeOffset.UtcNow.AddHours(2),
                DeletedAt = null,
                Addresses = new List<CustomerAddress>
                {
                    new CustomerAddress { Id = 1, City = "Cairo", Country = "EG", PhoneNumber = "010", State = "C", StreetAddress = "Street 1", AddressType = "Home", IsDefault = true },
                    new CustomerAddress { Id = 2, City = "Giza", Country = "EG", PhoneNumber = "011", State = "G", StreetAddress = "Street 2", AddressType = "Work", IsDefault = false },
                }
            };

            var sut = CreateSut();
            var dto = await sut.ToUserDto(customer);

            Assert.Equal(customer.Id, dto.Id);
            Assert.Equal(customer.Name, dto.Name);
            Assert.Equal(customer.Email, dto.Email);
            Assert.Equal(customer.UserName, dto.UserName);
            Assert.True(dto.IsLock);
            Assert.False(dto.IsDeleted);
            Assert.False(dto.IsActive);
            Assert.Equal(2, dto.customerAddresses.Count);

            CustomerAddressDto first = dto.customerAddresses[0];
            Assert.Equal(1, first.Id);
            Assert.Equal("Cairo", first.City);
            Assert.True(first.IsDefault);
        }
    }
}
