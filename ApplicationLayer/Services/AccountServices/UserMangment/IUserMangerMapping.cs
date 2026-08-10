using Application.DtoModels.AccountDtos;
using Domain.Models;
using static Application.Services.AccountServices.UserMangment.UserQueryServiece;

using Microsoft.AspNetCore.Identity;

namespace Application.Services.AccountServices.UserMangment
{
	public interface IUserMangerMapping
    {
        Task<List<Userdto>> ToUserDto(IQueryable<Customer> query);
        public Task<UserwithAddressdto> ToUserDto(Customer customer);
    }
}


