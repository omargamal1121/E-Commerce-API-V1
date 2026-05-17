using Application.DtoModels.AccountDtos;
using Domain.Models;
using static Application.Services.AccountServices.UserMangment.UserQueryServiece;

namespace Application.Services.AccountServices.UserMangment
{
	public interface IUserMangerMapping
    {
        List<Userdto> ToUserDto(IQueryable<Customer> query);
        public UserwithAddressdto ToUserDto(Customer customer);
    }
}


