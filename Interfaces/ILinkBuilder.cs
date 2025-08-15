using E_Commerce.DtoModels.Shared;

namespace E_Commerce.Interfaces
{
    public interface ILinkBuilder
    {
        List<LinkDto> GenerateLinks(int? id = null);
        List<LinkDto> MakeRelSelf(List<LinkDto> links, string rel);
    }

    public interface ICategoryLinkBuilder : ILinkBuilder { }
    public interface IProductLinkBuilder : ILinkBuilder { }
    public interface IWareHouseLinkBuilder : ILinkBuilder { }
    public interface IAccountLinkBuilder : ILinkBuilder { }
    public interface ISubCategoryLinkBuilder : ILinkBuilder { }
}
