using Application.Services.PromoCodeServices;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.Services.PromoCodeServices
{
    public class PromoCodeJobServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<IPromoCodeRepository> _mockRepo;
        private readonly Mock<ILogger<PromoCodeJobService>> _mockLogger;
        private readonly PromoCodeJobService _sut;

        public PromoCodeJobServiceTests()
        {
            _mockUow = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IPromoCodeRepository>();
            _mockLogger = new Mock<ILogger<PromoCodeJobService>>();

            _mockUow.Setup(u => u.PromoCode).Returns(_mockRepo.Object);
            _sut = new PromoCodeJobService(_mockUow.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ActivatePromoCodeJobAsync_ValidPromoCode_ActivatesAndCommits()
        {
            var promo = new PromoCode
            {
                Id = 10,
                Code = "ACTIVATE_ME",
                IsActive = false,
                StartsAt = DateTime.UtcNow.AddMinutes(-1),
                EndsAt = DateTime.UtcNow.AddDays(2)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(promo);

            await _sut.ActivatePromoCodeJobAsync(10);

            Assert.True(promo.IsActive);
            _mockRepo.Verify(r => r.Update(promo), Times.Once);
            _mockUow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ActivatePromoCodeJobAsync_ExpiredPromoCode_DoesNotActivate()
        {
            var promo = new PromoCode
            {
                Id = 11,
                Code = "EXPIRED_CODE",
                IsActive = false,
                StartsAt = DateTime.UtcNow.AddDays(-5),
                EndsAt = DateTime.UtcNow.AddDays(-1)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(promo);

            await _sut.ActivatePromoCodeJobAsync(11);

            Assert.False(promo.IsActive);
            _mockRepo.Verify(r => r.Update(It.IsAny<PromoCode>()), Times.Never);
            _mockUow.Verify(u => u.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task ActivatePromoCodeJobAsync_NotFoundOrDeleted_DoesNothing()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((PromoCode?)null);

            await _sut.ActivatePromoCodeJobAsync(99);

            _mockRepo.Verify(r => r.Update(It.IsAny<PromoCode>()), Times.Never);
            _mockUow.Verify(u => u.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task DeactivatePromoCodeJobAsync_ActivePromoCode_DeactivatesAndCommits()
        {
            var promo = new PromoCode
            {
                Id = 12,
                Code = "DEACTIVATE_ME",
                IsActive = true,
                StartsAt = DateTime.UtcNow.AddDays(-5),
                EndsAt = DateTime.UtcNow.AddMinutes(-1)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(12)).ReturnsAsync(promo);

            await _sut.DeactivatePromoCodeJobAsync(12);

            Assert.False(promo.IsActive);
            _mockRepo.Verify(r => r.Update(promo), Times.Once);
            _mockUow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task DeactivatePromoCodeJobAsync_AlreadyInactive_DoesNothing()
        {
            var promo = new PromoCode
            {
                Id = 13,
                Code = "ALREADY_OFF",
                IsActive = false,
                StartsAt = DateTime.UtcNow.AddDays(-5),
                EndsAt = DateTime.UtcNow.AddMinutes(-1)
            };

            _mockRepo.Setup(r => r.GetByIdAsync(13)).ReturnsAsync(promo);

            await _sut.DeactivatePromoCodeJobAsync(13);

            _mockRepo.Verify(r => r.Update(It.IsAny<PromoCode>()), Times.Never);
            _mockUow.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
