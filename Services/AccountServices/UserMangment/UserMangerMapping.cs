using E_Commerce.DtoModels.AccountDtos;
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
                    IsLock = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTime.Now,
                    IsDeleted = u.DeletedAt != null,
                    CreateAt= u.CreateAt,
                    LastVisit = u.LastVisit.HasValue ? u.LastVisit.Value : (DateTime?)null,
                    Roles = (from ur in _unitOfWork.context.UserRoles
                             join r in _unitOfWork.context.Roles on ur.RoleId equals r.Id
                             where ur.UserId == u.Id
                             select r.Name).ToList()
                })
                .ToList();

            return userDtos;
        }
		public UserwithAddressdto ToUserDto(Customer  customer)
        {
            var userDto = new UserwithAddressdto
            {
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                UserName = customer.UserName,
                Name = customer.Name,
                IsLock = customer.LockoutEnd.HasValue && customer.LockoutEnd > DateTimeOffset.UtcNow,

                Id = customer.Id,
                IsActive = customer.LockoutEnd.HasValue || customer.LockoutEnd <= DateTime.Now,
                IsDeleted = customer.DeletedAt != null,
                LastVisit = customer.LastVisit.HasValue ? customer.LastVisit.Value : (DateTime?)null,
                Roles = (from ur in _unitOfWork.context.UserRoles
                         join r in _unitOfWork.context.Roles on ur.RoleId equals r.Id
                         where ur.UserId == customer.Id
                         select r.Name).ToList()

                       ,
                customerAddresses= (from addr in _unitOfWork.context.CustomerAddresses
                                   where addr.CustomerId == customer.Id 
                                   select new DtoModels.CustomerAddressDtos.CustomerAddressDto
                                   {
                                       Id = addr.Id,
                                       City = addr.City,
                                        AdditionalNotes = addr.AdditionalNotes,
                                       State = addr.State,
                                         AddressType = addr.AddressType,
                                         StreetAddress = addr.StreetAddress,
                                         PhoneNumber = addr.PhoneNumber,

                                       Country = addr.Country,
                                       IsDefault = addr.IsDefault
                                   }).ToList()

            };

            return userDto;
        }

    }
}
