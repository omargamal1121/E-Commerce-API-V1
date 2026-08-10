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
                userDtos.Add(new Userdto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsLock = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTime.Now,
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
                Roles = roles.ToList(),

                customerAddresses = customer.Addresses.Select(addr => new CustomerAddressDto
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


