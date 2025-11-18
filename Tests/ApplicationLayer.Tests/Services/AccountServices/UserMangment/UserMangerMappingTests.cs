using System;
using System.Collections.Generic;
using System.Linq;
using ApplicationLayer.DtoModels.AccountDtos;
using ApplicationLayer.DtoModels.CustomerAddressDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.AccountServices.UserMangment;
using DomainLayer.Models;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.AccountServices.UserMangment
{
    public class UserMangerMappingTests
    {
        private static UserMangerMapping CreateSut()
        {
            return new UserMangerMapping(new Mock<IUnitOfWork>().Object);
        }

        [Fact]
        public void ToUserDto_EmptyQuery_ReturnsEmptyList()
        {
            var sut = CreateSut();
            var result = sut.ToUserDto(Enumerable.Empty<Customer>().AsQueryable());

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToUserDto_MapsBasicFieldsAndFlags()
        {
            var now = DateTimeOffset.UtcNow;
            var customers = new List<Customer>
            {
                new Customer
                {
                    Id = "u1", Name = "Alice", Email = "a@x.com", UserName = "alice",
                    PhoneNumber = "111", CreateAt = DateTime.UtcNow.AddDays(-5),
                    LockoutEnd = now.AddDays(1), // future
                    DeletedAt = null,
                },
                new Customer
                {
                    Id = "u2", Name = "Bob", Email = "b@x.com", UserName = "bob",
                    PhoneNumber = "222", CreateAt = DateTime.UtcNow.AddDays(-3),
                    LockoutEnd = now.AddDays(-1), // past
                    DeletedAt = DateTime.UtcNow.AddDays(-1),
                    LastVisit = DateTime.UtcNow.AddHours(-2)
                },
                new Customer
                {
                    Id = "u3", Name = "Carol", Email = "c@x.com", UserName = "carol",
                    PhoneNumber = "333", CreateAt = DateTime.UtcNow.AddDays(-1),
                    LockoutEnd = null,
                }
            };

            var sut = CreateSut();
            var result = sut.ToUserDto(customers.AsQueryable());

            Assert.Equal(3, result.Count);

            var r1 = result.First(r => r.Id == "u1");
            Assert.True(r1.IsLock);
            Assert.False(r1.IsDeleted);
            Assert.False(r1.IsActive); // has lockout in future

            var r2 = result.First(r => r.Id == "u2");
            Assert.False(r2.IsLock);
            Assert.True(r2.IsDeleted);
            Assert.True(r2.IsActive); // lockout in past
            Assert.NotNull(r2.LastVisit);

            var r3 = result.First(r => r.Id == "u3");
            Assert.False(r3.IsLock);
            Assert.True(r3.IsActive); // no lockout
        }

        [Fact]
        public void ToUserDto_SingleCustomerWithAddresses_MapsAllFieldsAndAddresses()
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
            var dto = sut.ToUserDto(customer);

            Assert.Equal(customer.Id, dto.Id);
            Assert.Equal(customer.Name, dto.Name);
            Assert.Equal(customer.Email, dto.Email);
            Assert.Equal(customer.UserName, dto.UserName);
            Assert.True(dto.IsLock);
            Assert.False(dto.IsDeleted);
            Assert.True(dto.IsActive); // per current implementation: HasValue OR <= now
            Assert.Equal(2, dto.customerAddresses.Count);

            CustomerAddressDto first = dto.customerAddresses[0];
            Assert.Equal(1, first.Id);
            Assert.Equal("Cairo", first.City);
            Assert.True(first.IsDefault);
        }

        [Fact]
        public void ToUserDto_CustomerLockoutEndPast_IsActiveTrue()
        {
            var customer = new Customer
            {
                Id = "u5",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5)
            };
            var sut = CreateSut();
            var dto = sut.ToUserDto(customer);

            Assert.True(dto.IsActive);
            Assert.False(dto.IsLock);
        }

        [Fact]
        public void ToUserDto_CustomerNoLockout_IsActiveFalse()
        {
            var customer = new Customer { Id = "u6", LockoutEnd = null };
            var sut = CreateSut();
            var dto = sut.ToUserDto(customer);

            Assert.False(dto.IsActive);
            Assert.False(dto.IsLock);
        }
    }
}