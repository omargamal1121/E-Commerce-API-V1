using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using static E_Commerce.Services.AccountServices.UserMangment.UserQueryServiece;

namespace E_Commerce.Services.AccountServices.UserMangment
{

    public class UserMangerMapping: IUserMangerMapping
    {
        private readonly IUnitOfWork _unitOfWork;
		public UserMangerMapping(IUnitOfWork unitOfWork)
		{
            _unitOfWork = unitOfWork;
        }
       
		public List<Userdto> ToUserDto(IQueryable<Customer> query)
        {
            var userDtos = query
                .Select(u => new Userdto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTime.Now,
                    IsDeleted = u.DeletedAt != null,
                    LastVisit = u.LastVisit.HasValue ? u.LastVisit.Value : (DateTime?)null,
                    Roles = (from ur in _unitOfWork.context.UserRoles
                             join r in _unitOfWork.context.Roles on ur.RoleId equals r.Id
                             where ur.UserId == u.Id
                             select r.Name).ToList()
                })
                .ToList();

            return userDtos;
        }
		public Userdto ToUserDto(Customer  customer)
        {
            var userDto = new Userdto
            {
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Id = customer.Id,
                IsActive = customer.LockoutEnd.HasValue || customer.LockoutEnd <= DateTime.Now,
                IsDeleted = customer.DeletedAt != null,
                LastVisit = customer.LastVisit.HasValue ? customer.LastVisit.Value : (DateTime?)null,
                Roles = (from ur in _unitOfWork.context.UserRoles
                         join r in _unitOfWork.context.Roles on ur.RoleId equals r.Id
                         where ur.UserId == customer.Id
                         select r.Name).ToList()

            };

            return userDto;
        }

    }
}
