using Microsoft.VisualStudio.TestPlatform.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;

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
			var response= await  _client.GetAsync("api/Products");
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		}
	}
}
