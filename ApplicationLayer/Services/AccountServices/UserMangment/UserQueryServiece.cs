using ApplicationLayer.DtoModels.AccountDtos;
using DomainLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ApplicationLayer.Interfaces;

namespace ApplicationLayer.Services.AccountServices.UserMangment
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

		public async Task<Result<UserwithAddressdto>> GetUserByIdAsnyc(string  id)
		{
			var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
			if (user == null)
				return Result<UserwithAddressdto>.Fail("No User With this id",404);
			var userDto = _userMangerMapping.ToUserDto(user);
			return Result<UserwithAddressdto>.Ok(userDto);
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
     
                var usersInRole = _userManager.GetUsersInRoleAsync(role).GetAwaiter().GetResult();
                var ids = usersInRole.Select(u => u.Id).ToHashSet();
                query = query.Where(u => ids.Contains(u.Id));
            }
			query= query.Skip((page-1)*pageSize).Take(pageSize);
			var users = _userMangerMapping.ToUserDto(query);
			
            return Result<List<Userdto>>.Ok(users);



        }
	}
}


