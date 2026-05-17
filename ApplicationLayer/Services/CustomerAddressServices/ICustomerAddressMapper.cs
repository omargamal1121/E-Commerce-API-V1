using System.Collections.Generic;
using Application.DtoModels.CustomerAddressDtos;

namespace Application.Services.CustomerAddressServices
{
    public interface ICustomerAddressMapper
    {
        CustomerAddressDto ToDto(Domain.Models.CustomerAddress address);
        List<CustomerAddressDto> ToDtos(IEnumerable<Domain.Models.CustomerAddress> addresses);
        Domain.Models.CustomerAddress ToEntity(CreateCustomerAddressDto dto, string userId);
        void ApplyUpdates(Domain.Models.CustomerAddress entity, UpdateCustomerAddressDto dto);
    }
}