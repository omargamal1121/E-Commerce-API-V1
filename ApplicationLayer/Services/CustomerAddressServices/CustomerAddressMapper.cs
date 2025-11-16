
using ApplicationLayer.DtoModels.CustomerAddressDtos;
using DomainLayer.Models;

namespace ApplicationLayer.Services.CustomerAddressServices
{
    public class CustomerAddressMapper : ICustomerAddressMapper
    {
        public CustomerAddressDto ToDto(DomainLayer.Models.CustomerAddress address)
        {
            if (address == null) return null!;
            return new CustomerAddressDto
            {
                Id = address.Id,
                CustomerId = address.CustomerId,
                PhoneNumber = address.PhoneNumber,
                Country = address.Country,
                State = address.State,
                City = address.City,
                StreetAddress = address.StreetAddress,
                ApartmentSuite = null,
                PostalCode = address.PostalCode,
                AddressType = address.AddressType,
                IsDefault = address.IsDefault,
                AdditionalNotes = address.AdditionalNotes,
                CreatedAt = address.CreatedAt,
                ModifiedAt = address.ModifiedAt,
                DeletedAt = address.DeletedAt
            };
        }

        public List<CustomerAddressDto> ToDtos(IEnumerable<DomainLayer.Models.CustomerAddress> addresses)
        {
            if (addresses == null) return new List<CustomerAddressDto>();
            return addresses.Select(ToDto).ToList();
        }

        public DomainLayer.Models.CustomerAddress ToEntity(CreateCustomerAddressDto dto, string userId)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId is required", nameof(userId));

			return new CustomerAddress
			{
                CustomerId = userId,
                PhoneNumber = dto.PhoneNumber,
                Country = dto.Country,
                State = dto.State,
                City = dto.City,
                StreetAddress = dto.StreetAddress,
                PostalCode = dto.PostalCode,
                IsDefault = dto.IsDefault,
                AdditionalNotes = dto.AdditionalNotes,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void ApplyUpdates(DomainLayer.Models.CustomerAddress entity, UpdateCustomerAddressDto dto)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            if (!string.IsNullOrEmpty(dto.PhoneNumber))
                entity.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrEmpty(dto.Country))
                entity.Country = dto.Country;
            if (!string.IsNullOrEmpty(dto.State))
                entity.State = dto.State;
            if (!string.IsNullOrEmpty(dto.City))
                entity.City = dto.City;
            if (!string.IsNullOrEmpty(dto.StreetAddress))
                entity.StreetAddress = dto.StreetAddress;
            if (!string.IsNullOrEmpty(dto.PostalCode))
                entity.PostalCode = dto.PostalCode;
            if (!string.IsNullOrEmpty(dto.AddressType))
                entity.AddressType = dto.AddressType;
            if (dto.AdditionalNotes != null)
                entity.AdditionalNotes = dto.AdditionalNotes;
            // IsDefault handling is business logic in service; not set here.
        }
    }
}