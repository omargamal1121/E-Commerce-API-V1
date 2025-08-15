using AutoMapper;
using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.WareHouseDtos;
using E_Commerce.Models;
using E_Commerce.DtoModels.CustomerAddressDtos;
using E_Commerce.DtoModels.SubCategorydto;

namespace E_Commerce.Mappings
{
	public class MappingProfile:Profile
	{
		public MappingProfile()
		{
			//CreateMap<Product,ProductDto>().ForMember(c => c.FinalPrice, op => op.MapFrom(c => c.Discount==null?c.Price: c.Price - c.Discount.DiscountPercent * c.Price)).ForMember(p=>p.AvailabeQuantity,op=>op.MapFrom(p=>p.InventoryEntries.Sum(x=>x.Quantity))).ReverseMap();
			CreateMap<Category, CategoryDto>()

				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
				.ForMember(dest => dest.DisplayOrder, opt => opt.MapFrom(src => src.DisplayOrder))
				.ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
				.ForMember(dest => dest.DeletedAt, opt => opt.MapFrom(src => src.DeletedAt))
				.ForMember(dest => dest.ModifiedAt, opt => opt.MapFrom(src => src.ModifiedAt))
				.AfterMap((src, dest) =>
				{


				})
				.ReverseMap();


			CreateMap<CreateCategotyDto, Category>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description.Trim()))
				.ForMember(dest => dest.DisplayOrder, opt => opt.MapFrom(src => src.DisplayOrder))
				.ForMember(dest => dest.Images, opt => opt.Ignore())
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
				.ForMember(dest => dest.SubCategories, opt => opt.Ignore());
			CreateMap<UpdateCategoryDto, Category>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description.Trim()))
				.ForMember(dest => dest.Id, opt => opt.Ignore()) // Don't update ID
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Don't update creation date
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore()) // Will be set by BaseEntity
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore()) // Don't update deletion date
				.ForMember(dest => dest.Images, opt => opt.Ignore()) // Don't update images
				.ForMember(dest => dest.SubCategories, opt => opt.Ignore()); // Don't update subcategories

			CreateMap<CreateCategotyDto, CategoryDto>().ReverseMap();
			CreateMap<RegisterDto, Customer>().ReverseMap();
			CreateMap<RegisterDto, RegisterResponse>().ReverseMap();
			CreateMap<WareHouseDto, Warehouse>().ReverseMap();
			CreateMap<Customer, RegisterResponse>();
			
			CreateMap<ProductInventory, InventoryDto>()
				.ForMember(dest => dest.Quantityinsidewarehouse, opt => opt.MapFrom(src => src.Quantity))
				.ForMember(dest => dest.WareHousid, opt => opt.MapFrom(src => src.WarehouseId))
				.ForMember(dest => dest.Product, opt => opt.MapFrom(src => src.Product));

			// Product mappings
			CreateMap<CreateProductDto, Product>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))

				.ForMember(dest => dest.SubCategoryId, opt => opt.MapFrom(src => src.Subcategoryid));

			CreateMap<UpdateProductDto, Product>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))

				.ForMember(dest => dest.SubCategoryId, opt => opt.MapFrom(src => src.SubCategoryid));

			CreateMap<Product, ProductDto>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
				.ForMember(dest => dest.AvailableQuantity, opt => opt.MapFrom(src => src.Quantity))

				.ForMember(dest => dest.FinalPrice, opt => opt.MapFrom(src => CalculateFinalPrice(src)));



			CreateMap<CreateProductVariantDto, ProductVariant>();
			CreateMap<UpdateProductVariantDto, ProductVariant>();
			CreateMap<ProductVariant, ProductVariantDto>();

			// Cart mappings
			CreateMap<Cart, CartDto>()
				.ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
				
				.ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
				.ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
				.ForMember(dest => dest.TotalItems, opt => opt.MapFrom(src => src.TotalItems))
				.ForMember(dest => dest.IsEmpty, opt => opt.MapFrom(src => src.IsEmpty));

			CreateMap<CartItem, CartItemDto>()
				.ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
				.ForMember(dest => dest.Product, opt => opt.MapFrom(src => src.Product))

				.ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => CalculateUnitPrice(src)))
				
				.ForMember(dest => dest.AddedAt, opt => opt.MapFrom(src => src.AddedAt));

			
			// Order mappings
			CreateMap<Order, OrderDto>()
				.ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src => src.OrderNumber))
			
				.ForMember(dest => dest.Customer, opt => opt.MapFrom(src => src.Customer))
				.ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
				.ForMember(dest => dest.Subtotal, opt => opt.MapFrom(src => src.Subtotal))
				.ForMember(dest => dest.TaxAmount, opt => opt.MapFrom(src => src.TaxAmount))
				.ForMember(dest => dest.ShippingCost, opt => opt.MapFrom(src => src.ShippingCost))
				.ForMember(dest => dest.DiscountAmount, opt => opt.MapFrom(src => src.DiscountAmount))
				.ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Total))
				.ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes))
				.ForMember(dest => dest.ShippedAt, opt => opt.MapFrom(src => src.ShippedAt))
				.ForMember(dest => dest.DeliveredAt, opt => opt.MapFrom(src => src.DeliveredAt))
				.ForMember(dest => dest.CancelledAt, opt => opt.MapFrom(src => src.CancelledAt))
				.ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
				.ForMember(dest => dest.Payment, opt => opt.MapFrom(src => src.Payment));

			CreateMap<OrderItem, OrderItemDto>()
				.ForMember(dest => dest.Product, opt => opt.MapFrom(src => src.Product))
				

				.ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
				.ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
				.ForMember(dest => dest.OrderedAt, opt => opt.MapFrom(src => src.OrderedAt));

			CreateMap<Payment, PaymentDto>()
				.ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId))
				.ForMember(dest => dest.PaymentMethodId, opt => opt.MapFrom(src => src.PaymentMethodId))
				.ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod))
				.ForMember(dest => dest.PaymentProviderId, opt => opt.MapFrom(src => src.PaymentProviderId))
				.ForMember(dest => dest.PaymentProvider, opt => opt.MapFrom(src => src.PaymentProvider))
				.ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
				.ForMember(dest => dest.PaymentDate, opt => opt.MapFrom(src => src.PaymentDate))
				.ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
				.ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status));

		


			CreateMap<Collection, CollectionDto>()

				.ForMember(dest => dest.Images, opt => opt.Ignore())
				.ForMember(dest => dest.Products, opt => opt.MapFrom(src => src.ProductCollections.Select(pc => pc.Product)))
				.ForMember(dest => dest.TotalProducts, opt => opt.MapFrom(src => src.ProductCollections.Count));
				

			CreateMap<CreateCollectionDto, Collection>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
				.ForMember(dest => dest.DisplayOrder, opt => opt.MapFrom(src => src.DisplayOrder))
			
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ProductCollections, opt => opt.Ignore())
				.ForMember(dest => dest.Images, opt => opt.Ignore());

			CreateMap<UpdateCollectionDto, Collection>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
				.ForMember(dest => dest.DisplayOrder, opt => opt.MapFrom(src => src.DisplayOrder))
	
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ProductCollections, opt => opt.Ignore())
				.ForMember(dest => dest.Images, opt => opt.Ignore());

			CreateMap<Collection, CollectionSummaryDto>()

				.ForMember(dest => dest.TotalProducts, opt => opt.MapFrom(src => src.ProductCollections.Count));
			

			// CustomerAddress mappings
			CreateMap<CustomerAddress, CustomerAddressDto>()
				.ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId))

				.ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
				.ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Country))
				.ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State))
				.ForMember(dest => dest.City, opt => opt.MapFrom(src => src.City))
				.ForMember(dest => dest.StreetAddress, opt => opt.MapFrom(src => src.StreetAddress))

				.ForMember(dest => dest.PostalCode, opt => opt.MapFrom(src => src.PostalCode))
				.ForMember(dest => dest.AddressType, opt => opt.MapFrom(src => src.AddressType))
				.ForMember(dest => dest.IsDefault, opt => opt.MapFrom(src => src.IsDefault))
				.ForMember(dest => dest.AdditionalNotes, opt => opt.MapFrom(src => src.AdditionalNotes))

				.ForMember(dest => dest.FullAddress, opt => opt.MapFrom(src => src.FullAddress));

			CreateMap<CreateCustomerAddressDto, CustomerAddress>()

				.ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber.Trim()))
				.ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Country.Trim()))
				.ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.Trim()))
				.ForMember(dest => dest.City, opt => opt.MapFrom(src => src.City.Trim()))
				.ForMember(dest => dest.StreetAddress, opt => opt.MapFrom(src => src.StreetAddress.Trim()))
				.ForMember(dest => dest.PostalCode, opt => opt.MapFrom(src => src.PostalCode.Trim()))
			
				.ForMember(dest => dest.IsDefault, opt => opt.MapFrom(src => src.IsDefault))
				.ForMember(dest => dest.AdditionalNotes, opt => opt.MapFrom(src => src.AdditionalNotes))
				.ForMember(dest => dest.CustomerId, opt => opt.Ignore())
				.ForMember(dest => dest.Customer, opt => opt.Ignore())
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore());

			// Image mappings
			CreateMap<Image, ImageDto>()
				.ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
				.ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.Url));

			CreateMap<SubCategory, SubCategoryDto>()

				.ForMember(dest => dest.Images, opt => opt.Ignore())

				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
				.ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
				.ForMember(dest => dest.DeletedAt, opt => opt.MapFrom(src => src.DeletedAt))
				.ForMember(dest => dest.ModifiedAt, opt => opt.MapFrom(src => src.ModifiedAt));




			CreateMap<CreateSubCategoryDto, SubCategory>()
				.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
				.ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description.Trim()))
				.ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))

				.ForMember(dest => dest.Images, opt => opt.Ignore())
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
				.ForMember(dest => dest.Category, opt => opt.Ignore())
				.ForMember(dest => dest.Products, opt => opt.Ignore());
			CreateMap<UpdateSubCategoryDto, SubCategory>()
				.ForMember(dest => dest.Images, opt => opt.Ignore())
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
				.ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
				.ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
				.ForMember(dest => dest.Category, opt => opt.Ignore())
				.ForMember(dest => dest.Products, opt => opt.Ignore()); 
		}
			
			
				

		private static decimal CalculateFinalPrice(Product product)
		{
			// If no variants, return 0
			if (!product.ProductVariants.Any())
				return 0;

			var minPrice = product.Price;
			
			// Apply discount if available
			if (product.Discount != null && product.Discount.IsActive)
			{
				minPrice = minPrice * (1 - product.Discount.DiscountPercent);
			}
			
			return Math.Round(minPrice, 2);
		}

		private static decimal CalculateMinPrice(Product product)
		{
			if (!product.ProductVariants.Any())
				return 0;
			
			return Math.Round(product.Price, 2);
		}

		private static decimal CalculateMaxPrice(Product product)
		{
			if (!product.ProductVariants.Any())
				return 0;
			
			return Math.Round(product.Price, 2);
		}

		private static decimal CalculateUnitPrice(CartItem cartItem)
		{
			
			
			if (cartItem.Product?.ProductVariants?.Any() == true)
			{
				return cartItem.Product.Price;
			}
			
			return 0;
		}

		private static decimal CalculateCartItemTotalPrice(CartItem cartItem)
		{
			var unitPrice = CalculateUnitPrice(cartItem);
			var quantity = cartItem.Quantity;
			
			// Apply discount if available
			if (cartItem.Product?.Discount != null && cartItem.Product.Discount.IsActive)
			{
				unitPrice *= (1 - cartItem.Product.Discount.DiscountPercent / 100);
			}
			
			return unitPrice * quantity;
		}
	}
}
