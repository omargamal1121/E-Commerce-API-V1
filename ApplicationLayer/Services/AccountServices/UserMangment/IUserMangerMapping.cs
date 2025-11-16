using ApplicationLayer.DtoModels.AccountDtos;
using DomainLayer.Models;
using static ApplicationLayer.Services.AccountServices.UserMangment.UserQueryServiece;

namespace ApplicationLayer.Services.AccountServices.UserMangment
{
	public interface IUserMangerMapping
    {
        List<Userdto> ToUserDto(IQueryable<Customer> query);
        public UserwithAddressdto ToUserDto(Customer customer);
    }
}


