using Application.DtoModels.AccountDtos;
using Domain.Enums;
using Domain.Models;

namespace Application.Interfaces
{
	public interface ICustomerFactory
    {
		public Customer CreateCustomer(RegisterDto registerDto);

    }
}
