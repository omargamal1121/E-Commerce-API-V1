using E_Commerce.Models;
using static E_Commerce.Services.AccountServices.UserMangment.UserQueryServiece;

namespace E_Commerce.Services.AccountServices.UserMangment
{
	public interface IUserMangerMapping
    {
        List<Userdto> ToUserDto(IQueryable<Customer> query);
        public Userdto ToUserDto(Customer customer);
    }
}
