using E_Commerce.DtoModels.Shared;

namespace E_Commerce.Interfaces
{
    public interface IProductInventoryLinkBuilder : ILinkBuilder
    {
        List<LinkDto> GenerateLinks(int? id = null);
    }
} 