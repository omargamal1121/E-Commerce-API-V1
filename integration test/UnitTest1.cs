using ApplicationLayer.DtoModels;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.DtoModels.TokenDtos;
using DomainLayer;
using InfrastructureLayer.Context;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTest
{
    public class UnitTest1 : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        public UnitTest1(WebApplicationFactory<Program> webApplicationFactory)
        {

            
            _client = webApplicationFactory.CreateClient();
       

        }
        [Fact]
        public async Task Test1()
        {
           
            var response = await _client.GetAsync("api/Products?page=1&pageSize=5");
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
            
            Assert.Equal(5, content?.ResponseBody.Data?.Count);

        }
        [Fact]
        public async Task Test2()
        {
            var dto = new
            {
                name = "96RvkAwQ5WTVclew5Awp",
                description = "stringstri",
                subcategoryid = 1,
                fitType = 0,
                gender = 0,
                price = 100
            };

            var response = await _client.PostAsJsonAsync("api/Products", dto);

           

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task Test3()
        {
            var dto = new
            {
                email = "Omargamal1132004@gmail.com",
                password = "Admin@123",
                
            };

            var response = await _client.PostAsJsonAsync("api/Account/login", dto);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<TokensDto>>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var dto2 = new
            {
                name = "96RvkAwQ5WTVclew5Awp",
                description = "stringstri",
                subcategoryid = 1,
                fitType = 0,
                gender = 0,
                price = 100
            };
            var token = content?.ResponseBody.Data?.Token;
            var request = new HttpRequestMessage(HttpMethod.Post, "api/Products");
            request.Content = JsonContent.Create(dto2);
            request.Headers.Add("Authorization", $"Bearer {token}");


            var response2 = await _client.SendAsync(request);




            Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        }

    }
}
