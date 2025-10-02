using E_Commerce.DtoModels.AdminOpreationDtos;
using E_Commerce.Enums;
using E_Commerce.Models;

namespace E_Commerce.Services.AdminOperationServices
{
	public interface IAdminOpreationServices
	{
		Task<Result< AdminOperationsLog>> AddAdminOpreationAsync(string description, Opreations opreation, string userid, int itemid);
		Task<Result<AdminOperationsLog>> AddAdminOpreationAsync(string description, Opreations opreation, string userid, List<int> itemids);
		Task<Result<List<OpreationDto>>> GetAllOpreationsAsync(string? userid = null, string? name = null, Opreations? opreation = null);


	}
}
