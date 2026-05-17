using Application.DtoModels.AccountDtos;
using Application.Services;
using static Application.Services.AccountServices.UserMangment.UserQueryServiece;

namespace Application.Services.AccountServices.UserMangment
{
	public interface IUserQueryServiece
	{
		Task< Result<UserwithAddressdto>> GetUserByIdAsnyc(string id);
		public Result<List<Userdto>> FilterUsers(
		string? name = null,
		string? email = null,
		string? role = null,
		string? phonenumber = null,
		bool? IsActive = null,
		bool? isDeleted = null,
		int page = 1, int pageSize = 10);


    }
}


