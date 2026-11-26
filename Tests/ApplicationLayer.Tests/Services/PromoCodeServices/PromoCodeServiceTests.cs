using System.Linq.Expressions;
using Application.DtoModels.PromoCodeDtos;
using Application.Services.PromoCodeServices;
using Domain.Enums;
using Domain.Models;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.Services.PromoCodeServices
{
    public class PromoCodeServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<IPromoCodeRepository> _mockRepo;
        private readonly Mock<ILogger<PromoCodeService>> _mockLogger;
        private readonly Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private readonly PromoCodeService _sut;

        public PromoCodeServiceTests()
        {
            _mockUow = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IPromoCodeRepository>();
            _mockLogger = new Mock<ILogger<PromoCodeService>>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();

            _mockUow.Setup(u => u.PromoCode).Returns(_mockRepo.Object);
            _sut = new PromoCodeService(_mockUow.Object, _mockLogger.Object, _mockBackgroundJobClient.Object);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task ValidatePromoCodeAsync_NullOrEmpty_ReturnsNotFound(string? code)
        {
            var result = await _sut.ValidatePromoCodeAsync(code);

            Assert.False(result.IsValid);
            Assert.Equal("NotFound", result.Reason);
        }

        [Theory]
        [InlineData("AB")] // Too short (< 3)
        [InlineData("TOOLONGCODE_GREATER_THAN_TWENTY_CHARACTERS")] // > 20
        [InlineData("PROMO@10")] // Invalid character '@'
        public async Task ValidatePromoCodeAsync_InvalidFormat_ReturnsNotFound(string code)
        {
            var result = await _sut.ValidatePromoCodeAsync(code);

            Assert.False(result.IsValid);
            Assert.Equal("NotFound", result.Reason);
        }

        [Fact]
        public async Task ValidatePromoCodeAsync_NonExistent_ReturnsNotFound()
        {
            _mockRepo.Setup(r => r.GetByCodeAsync("SAVE20", false, true))
                .ReturnsAsync((PromoCode?)null);

            var result = await _sut.ValidatePromoCodeAsync("save20");

            Assert.False(result.IsValid);
            Assert.Equal("NotFound", result.Reason);
        }

        [Fact]
        public async Task ValidatePromoCodeAsync_Inactive_ReturnsInactive()
        {
            var promo = new PromoCode
            {
                Id = 1,
                Code = "SAVE20",
                DiscountPercent = 20m,
                IsActive = false,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddDays(1)
            };

            _mockRepo.Setup(r => r.GetByCodeAsync("SAVE20", false, true))
                .ReturnsAsync(promo);

            var result = await _sut.ValidatePromoCodeAsync("SAVE20");

            Assert.False(result.IsValid);
            Assert.Equal("Inactive", result.Reason);
        }

        [Fact]
        public async Task ValidatePromoCodeAsync_NotStarted_ReturnsNotStarted()
        {
            var promo = new PromoCode
            {
                Id = 1,
                Code = "FUTURE10",
                DiscountPercent = 10m,
                IsActive = true,
                StartsAt = DateTime.UtcNow.AddDays(1),
                EndsAt = DateTime.UtcNow.AddDays(5)
            };

            _mockRepo.Setup(r => r.GetByCodeAsync("FUTURE10", false, true))
                .ReturnsAsync(promo);

            var result = await _sut.ValidatePromoCodeAsync("future10");

            Assert.False(result.IsValid);
            Assert.Equal("NotStarted", result.Reason);
        }

        [Fact]
        public async Task ValidatePromoCodeAsync_Expired_ReturnsExpired()
        {
            var promo = new PromoCode
            {
                Id = 1,
                Code = "OLDPROMO",
                DiscountPercent = 15m,
                IsActive = true,
                StartsAt = DateTime.UtcNow.AddDays(-10),
                EndsAt = DateTime.UtcNow.AddMinutes(-5)
            };

            _mockRepo.Setup(r => r.GetByCodeAsync("OLDPROMO", false, true))
                .ReturnsAsync(promo);

            var result = await _sut.ValidatePromoCodeAsync("oldpromo");

            Assert.False(result.IsValid);
            Assert.Equal("Expired", result.Reason);
        }

        [Fact]
        public async Task ValidatePromoCodeAsync_Valid_ReturnsSuccessWithNormalizedCode()
        {
            var promo = new PromoCode
            {
                Id = 5,
                Code = "SUMMER2026",
                DiscountPercent = 25m,
                IsActive = true,
                StartsAt = DateTime.UtcNow.AddDays(-2),
                EndsAt = DateTime.UtcNow.AddDays(10)
            };

            _mockRepo.Setup(r => r.GetByCodeAsync("SUMMER2026", false, true))
                .ReturnsAsync(promo);

            var result = await _sut.ValidatePromoCodeAsync("  summer2026  ");

            Assert.True(result.IsValid);
            Assert.Equal(5, result.PromoCodeId);
            Assert.Equal("SUMMER2026", result.Code);
            Assert.Equal(25m, result.DiscountPercent);
            Assert.Null(result.Reason);
        }

        [Fact]
        public async Task CreateAsync_Valid_ReturnsCreated()
        {
            var dto = new CreatePromoCodeDto
            {
                Code = "winter_50",
                DiscountPercent = 50m,
                StartsAt = DateTime.UtcNow,
                EndsAt = DateTime.UtcNow.AddDays(7),
                IsActive = true
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("WINTER_50", null))
                .ReturnsAsync(false);

            var result = await _sut.CreateAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal("WINTER_50", result.Data!.Code);
            _mockRepo.Verify(r => r.CreateAsync(It.Is<PromoCode>(p => p.Code == "WINTER_50" && p.DiscountPercent == 50m)), Times.Once);
            _mockUow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_EndsAtBeforeStartsAt_ReturnsBadRequest()
        {
            var dto = new CreatePromoCodeDto
            {
                Code = "INVALID_DATE",
                DiscountPercent = 20m,
                StartsAt = DateTime.UtcNow.AddDays(2),
                EndsAt = DateTime.UtcNow.AddDays(1),
                IsActive = true
            };

            var result = await _sut.CreateAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task CreateAsync_EndsAtInPast_ReturnsBadRequest()
        {
            var dto = new CreatePromoCodeDto
            {
                Code = "PAST_PROMO",
                DiscountPercent = 20m,
                StartsAt = DateTime.UtcNow.AddDays(-5),
                EndsAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true
            };

            var result = await _sut.CreateAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task CreateAsync_DuplicateCode_ReturnsConflict()
        {
            var dto = new CreatePromoCodeDto
            {
                Code = "DUPLICATE",
                DiscountPercent = 10m,
                StartsAt = DateTime.UtcNow,
                EndsAt = DateTime.UtcNow.AddDays(5),
                IsActive = true
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("DUPLICATE", null))
                .ReturnsAsync(true);

            var result = await _sut.CreateAsync(dto);

            Assert.False(result.Success);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task ActivateAsync_ExpiredPromo_FailsWithClearMessage()
        {
            var promo = new PromoCode
            {
                Id = 10,
                Code = "EXPIRED_CODE",
                IsActive = false,
                StartsAt = DateTime.UtcNow.AddDays(-10),
                EndsAt = DateTime.UtcNow.AddMinutes(-1)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(10))
                .ReturnsAsync(promo);

            var result = await _sut.ActivateAsync(10);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("extend", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ActivateAsync_ValidPromo_SetsIsActiveTrue()
        {
            var promo = new PromoCode
            {
                Id = 11,
                Code = "ACTIVE_CODE",
                IsActive = false,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddDays(10)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(11))
                .ReturnsAsync(promo);

            var result = await _sut.ActivateAsync(11);

            Assert.True(result.Success);
            Assert.True(promo.IsActive);
            _mockUow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task DeactivateAsync_ValidPromo_SetsIsActiveFalse()
        {
            var promo = new PromoCode
            {
                Id = 12,
                Code = "ACTIVE_CODE",
                IsActive = true,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddDays(10)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(12))
                .ReturnsAsync(promo);

            var result = await _sut.DeactivateAsync(12);

            Assert.True(result.Success);
            Assert.False(promo.IsActive);
            _mockUow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ChangeCodeWhenUsedInOrders_ReturnsBadRequest()
        {
            var promo = new PromoCode
            {
                Id = 1,
                Code = "OLDCODE",
                DiscountPercent = 20m,
                StartsAt = DateTime.UtcNow,
                EndsAt = DateTime.UtcNow.AddDays(5)
            };

            var dto = new UpdatePromoCodeDto
            {
                Code = "NEWCODE",
                DiscountPercent = 25m,
                StartsAt = DateTime.UtcNow,
                EndsAt = DateTime.UtcNow.AddDays(5)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(promo);
            _mockRepo.Setup(r => r.GetOrderCountForPromoCodeAsync(1)).ReturnsAsync(3); // Used in 3 orders

            var result = await _sut.UpdateAsync(1, dto);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("used in existing orders", result.Message);
        }

        [Fact]
        public async Task GenerateCodeAsync_ReturnsUnique8CharacterCode()
        {
            _mockRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), null))
                .ReturnsAsync(false);

            var result = await _sut.GenerateCodeAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(8, result.Data.Length);
            Assert.Matches(@"^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$", result.Data);
        }

        [Theory]
        [InlineData(100.0, 10.0, 10.0, 90.0)]
        [InlineData(199.99, 15.0, 30.00, 169.99)] // 199.99 * 0.15 = 29.9985 -> 30.00
        [InlineData(50.0, 100.0, 50.0, 0.0)]
        public void DiscountCalculation_RoundsAwayFromZeroAndNeverBelowZero(
            decimal subtotal, decimal percent, decimal expectedDiscount, decimal expectedTotal)
        {
            var discountAmount = Math.Round(subtotal * percent / 100m, 2, MidpointRounding.AwayFromZero);
            if (discountAmount > subtotal)
                discountAmount = subtotal;

            var total = Math.Max(0m, subtotal - discountAmount);

            Assert.Equal(expectedDiscount, discountAmount);
            Assert.Equal(expectedTotal, total);
        }

        [Fact]
        public async Task CreateAsync_StartsInFuture_SchedulesBothActivationAndDeactivationJobs()
        {
            var dto = new CreatePromoCodeDto
            {
                Code = "FUTURE_DEAL",
                DiscountPercent = 20m,
                StartsAt = DateTime.UtcNow.AddHours(2),
                EndsAt = DateTime.UtcNow.AddDays(3),
                IsActive = false
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("FUTURE_DEAL", null)).ReturnsAsync(false);
            _mockRepo.Setup(r => r.CreateAsync(It.IsAny<PromoCode>())).ReturnsAsync((PromoCode p) => p);
            _mockUow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

            var result = await _sut.CreateAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(PromoCodeStatus.Upcoming, result.Data!.Status);

            // Verifies 2 Hangfire scheduled jobs: 1 for activation, 1 for deactivation
            _mockBackgroundJobClient.Verify(
                x => x.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task CreateAsync_StartsNow_SchedulesDeactivationJobOnly()
        {
            var dto = new CreatePromoCodeDto
            {
                Code = "NOW_DEAL",
                DiscountPercent = 15m,
                StartsAt = DateTime.UtcNow.AddMinutes(-5),
                EndsAt = DateTime.UtcNow.AddDays(1),
                IsActive = true
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("NOW_DEAL", null)).ReturnsAsync(false);
            _mockRepo.Setup(r => r.CreateAsync(It.IsAny<PromoCode>())).ReturnsAsync((PromoCode p) => p);
            _mockUow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

            var result = await _sut.CreateAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(PromoCodeStatus.Active, result.Data!.Status);

            // Only 1 job scheduled (deactivation at EndsAt), since StartsAt is not in the future
            _mockBackgroundJobClient.Verify(
                x => x.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()),
                Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCorrectPromoCodeStatusEnum()
        {
            var promo = new PromoCode
            {
                Id = 99,
                Code = "CHECK_STATUS",
                DiscountPercent = 10m,
                IsActive = true,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddDays(2)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync(promo);
            _mockRepo.Setup(r => r.GetOrderCountForPromoCodeAsync(99)).ReturnsAsync(5);

            var result = await _sut.GetByIdAsync(99);

            Assert.True(result.Success);
            Assert.Equal(PromoCodeStatus.Active, result.Data!.Status);
            Assert.Equal(5, result.Data.OrdersCount);
        }

        [Fact]
        public async Task UpdateAsync_DatesUnchanged_DoesNotScheduleAnyHangfireJobs()
        {
            var startsAt = DateTime.UtcNow.AddHours(2);
            var endsAt = DateTime.UtcNow.AddDays(5);

            var promo = new PromoCode
            {
                Id = 1,
                Code = "UNCHANGED_DATES",
                DiscountPercent = 20m,
                StartsAt = startsAt,
                EndsAt = endsAt,
                IsActive = false
            };

            var dto = new UpdatePromoCodeDto
            {
                Code = "UNCHANGED_DATES",
                DiscountPercent = 25m, // only discount changed
                StartsAt = startsAt,
                EndsAt = endsAt,
                IsActive = true
            };

            _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(promo);
            _mockRepo.Setup(r => r.GetOrderCountForPromoCodeAsync(1)).ReturnsAsync(0);
            _mockUow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

            var result = await _sut.UpdateAsync(1, dto);

            Assert.True(result.Success);
            Assert.Equal(25m, promo.DiscountPercent);
            Assert.True(promo.IsActive);

            // Verify no Hangfire jobs were scheduled since dates did not change
            _mockBackgroundJobClient.Verify(
                x => x.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_OnlyEndsAtChanged_SchedulesDeactivationJobOnly()
        {
            var startsAt = DateTime.UtcNow.AddHours(-1); // already started
            var oldEndsAt = DateTime.UtcNow.AddDays(2);
            var newEndsAt = DateTime.UtcNow.AddDays(7);

            var promo = new PromoCode
            {
                Id = 2,
                Code = "EXTEND_END",
                DiscountPercent = 20m,
                StartsAt = startsAt,
                EndsAt = oldEndsAt,
                IsActive = true
            };

            var dto = new UpdatePromoCodeDto
            {
                Code = "EXTEND_END",
                DiscountPercent = 20m,
                StartsAt = startsAt,
                EndsAt = newEndsAt, // extended end date
                IsActive = true
            };

            _mockRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(promo);
            _mockRepo.Setup(r => r.GetOrderCountForPromoCodeAsync(2)).ReturnsAsync(0);
            _mockUow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

            var result = await _sut.UpdateAsync(2, dto);

            Assert.True(result.Success);
            Assert.Equal(newEndsAt, promo.EndsAt);

            // Exactly 1 job (deactivation) scheduled
            _mockBackgroundJobClient.Verify(
                x => x.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()),
                Times.Once);
        }
    }
}
