using E_Commerce.Enums;
using E_Commerce.Models;

namespace E_Commerce.Services.AdminOpreationServices
{
	public interface IAdminOpreationServices
	{
		public Task<Result< AdminOperationsLog>> AddAdminOpreationAsync(string description, Opreations opreation, string userid, int itemid);
		public Task<Result< bool>> DeleteAdminOpreationAsync(int id);
		public Task<Result<AdminOperationsLog>> AddAdminOpreationAsync(string description, Opreations opreation, string userid, List<int> itemids);
		public Task<Result< List<AdminOperationsLog>>> GetAllOpreationsAsync();
		public Task<Result<List<AdminOperationsLog>>> GetAllOpreationsByOpreationTypeAsync(Opreations opreation);
	}
}
