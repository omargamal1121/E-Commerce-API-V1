using ApplicationLayer.DtoModels.Shared;

namespace ApplicationLayer.Interfaces
{
    public interface IProductInventoryLinkBuilder : ILinkBuilder
    {
        List<LinkDto> GenerateLinks(int? id = null);
    }
} 

