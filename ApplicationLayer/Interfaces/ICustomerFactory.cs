using ApplicationLayer.DtoModels.AccountDtos;
using DomainLayer.Enums;
using DomainLayer.Models;

namespace ApplicationLayer.Interfaces
{
	public interface ICustomerFactory
    {
		public Customer CreateCustomer(RegisterDto registerDto);

    }
}
