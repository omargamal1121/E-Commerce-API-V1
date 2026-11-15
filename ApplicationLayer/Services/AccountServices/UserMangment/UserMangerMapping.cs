using Application.DtoModels.AccountDtos;
using Application.DtoModels.CustomerAddressDtos;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.AccountServices.UserMangment
{

    public class UserMangerMapping: IUserMangerMapping
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<Customer> _userManager;

		public UserMangerMapping(IUnitOfWork unitOfWork, UserManager<Customer> userManager)
		{
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }
       
		public async Task<List<Userdto>> ToUserDto(IQueryable<Customer> query)
        {
            var users = await query.ToListAsync();

            var userDtos = new List<Userdto>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var isLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
                userDtos.Add(new Userdto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsLock = isLocked,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = !isLocked,
                    IsDeleted = u.DeletedAt != null,
                    CreateAt = u.CreateAt,
                    LastVisit = u.LastVisit.HasValue ? u.LastVisit.Value : (DateTime?)null,
                    Roles = roles.ToList()
                });
            }

            return userDtos;
        }
		public async Task<UserwithAddressdto> ToUserDto(Customer  customer)
        {
            var roles = await _userManager.GetRolesAsync(customer);
            var isLocked = customer.LockoutEnd.HasValue && customer.LockoutEnd > DateTimeOffset.UtcNow;
            var userDto = new UserwithAddressdto
            {
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                UserName = customer.UserName,
                Name = customer.Name,
                IsLock = isLocked,

                Id = customer.Id,
                IsActive = !isLocked,
                IsDeleted = customer.DeletedAt != null,
                CreateAt = customer.CreateAt,
                LastVisit = customer.LastVisit.HasValue ? customer.LastVisit.Value : (DateTime?)null,
                Roles = roles.ToList(),

                customerAddresses = customer.Addresses?.Select(addr => new CustomerAddressDto
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
                }).ToList() ?? new List<CustomerAddressDto>()
            
            };

            return userDto;
        }

    }
}


