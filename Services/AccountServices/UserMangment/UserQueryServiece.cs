using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace E_Commerce.Services.AccountServices.UserMangment
{

    public partial class UserQueryServiece: IUserQueryServiece
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IUserMangerMapping _userMangerMapping;
        private readonly UserManager<Customer> _userManager;
        public UserQueryServiece(IUnitOfWork unitOfWork,UserManager<Customer> userManager,
			IUserMangerMapping userMangerMapping)
		{

			_userMangerMapping = userMangerMapping;
            _userManager = userManager;
            _unitOfWork = unitOfWork;

        }

		public async Task<Result< Userdto>> GetUserByIdAsnyc(string  id)
		{
			var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
			if (user == null)
				return Result<Userdto>.Fail("No User With this id",404);
			var userDto = _userMangerMapping.ToUserDto(user);
			return Result<Userdto>.Ok(userDto);
        }
		public Result< List<Userdto>> FilterUsers(
			string? name = null, 
			string? email=null,
			string? role=null,
			string? phonenumber=null,
			bool? IsActive=null,
			bool ? isDeleted=null,
			int page=1,int pageSize=10)
		{
			var query = _userManager.Users.AsNoTracking();
			if(name!=null)
				query=query.Where(u=>u.Name.Contains(name));
			if(email!=null)
                query = query.Where(u => u.Email != null && u.Email.Contains(email));
            if (phonenumber!=null)
				query=query.Where(u=>u.PhoneNumber!=null&&u.PhoneNumber.Contains(phonenumber));
			if(IsActive!=null){
				if (IsActive.Value)
					query = query.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTime.Now);
                else
                    query = query.Where(u => u.LockoutEnd.HasValue);

            }
			if (isDeleted != null)
			{
				if (isDeleted.Value)
					query = query.Where(u => u.DeletedAt != null);
				else
					query = query.Where(u => u.DeletedAt == null);
            }
            if (role != null)
            {
                query = from u in query
                        join ur in _unitOfWork.context.UserRoles on u.Id equals ur.UserId
                        join r in _unitOfWork.context.Roles on ur.RoleId equals r.Id
                        where r.Name == role
                        select u;
            }
			query= query.Skip((page-1)*pageSize).Take(pageSize);
			var users = _userMangerMapping.ToUserDto(query);
            return Result<List<Userdto>>.Ok(users);



        }
	}
}
