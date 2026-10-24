using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DtoModels.CategoryDtos;
using Application.Services.CacheServices;
using Application.Services.CategoryServices;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests.Services.CacheServices
{
    public class InMemoryCacheDiagnosticTests
    {
        [Fact]
        public async Task TestDirectCache_ShouldWork()
        {
            var cache = new InMemoryCacheManager(NullLogger<InMemoryCacheManager>.Instance);
            var list = new List<CategoryDto> { new CategoryDto { Id = 1, Name = "Cat1" } };

            await cache.SetAsync("cat:key", list, TimeSpan.FromMinutes(30), new[] { "cat_tag" });
            var result = await cache.GetAsync<List<CategoryDto>>("cat:key");

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Cat1", result[0].Name);
        }

        [Fact]
        public void TestHangfireEnqueue_WhatHappensWithCapturedCacheManager()
        {
            GlobalConfiguration.Configuration.UseMemoryStorage();
            var jobClient = new BackgroundJobClient();
            var cache = new InMemoryCacheManager(NullLogger<InMemoryCacheManager>.Instance);
            var list = new List<CategoryDto> { new CategoryDto { Id = 1, Name = "Cat1" } };

            // This is exactly what CategoryCacheHelper.SetCategoryListCacheAsync does:
            // _jobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, data, expiration, new string[] { CACHELIST }));
            var ex = Record.Exception(() =>
            {
                jobClient.Enqueue(() => cache.SetAsync("cat:key", list, TimeSpan.FromMinutes(30), new[] { "cat_tag" }));
            });

            // Let's see if jobClient.Enqueue throws or what it creates
            Assert.Null(ex);
        }
    }
}
