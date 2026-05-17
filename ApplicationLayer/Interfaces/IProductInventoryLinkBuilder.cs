using Application.DtoModels.Shared;

namespace Application.Interfaces
{
    public interface IProductInventoryLinkBuilder : ILinkBuilder
    {
        List<LinkDto> GenerateLinks(int? id = null);
    }
} 

