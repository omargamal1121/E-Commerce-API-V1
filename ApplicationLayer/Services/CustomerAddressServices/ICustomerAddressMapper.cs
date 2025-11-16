using System.Collections.Generic;
using ApplicationLayer.DtoModels.CustomerAddressDtos;

namespace ApplicationLayer.Services.CustomerAddressServices
{
    public interface ICustomerAddressMapper
    {
        CustomerAddressDto ToDto(DomainLayer.Models.CustomerAddress address);
        List<CustomerAddressDto> ToDtos(IEnumerable<DomainLayer.Models.CustomerAddress> addresses);
        DomainLayer.Models.CustomerAddress ToEntity(CreateCustomerAddressDto dto, string userId);
        void ApplyUpdates(DomainLayer.Models.CustomerAddress entity, UpdateCustomerAddressDto dto);
    }
}