using Application.DtoModels.AccountDtos;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Factory.CustomersFactory
{
	public class CustomerFactory: ICustomerFactory
    {
		public  Customer CreateCustomer(RegisterDto register)
		{
			return new Customer
			{
				UserName = register.UserName,
				Email = register.Email,
				Name = register.Name,
				PhoneNumber = register.PhoneNumber,
				EmailConfirmed = false,
				LockoutEnabled = true,
				CreateAt = DateTime.UtcNow,
				Gender = register.Gender,
				
            };
        }
    }
}
