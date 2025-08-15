using E_Commerce.DtoModels.Shared;
using E_Commerce.Interfaces;

namespace E_Commerce.Services.CategoryServcies
{
    public abstract class BaseLinkBuilder 
    {
        protected readonly LinkGenerator _generator;
        protected readonly IHttpContextAccessor _context;
        protected abstract string ControllerName { get; }

        protected BaseLinkBuilder(IHttpContextAccessor context, LinkGenerator generator)
        {
            _context = context;
            _generator = generator;
        }

        public abstract List<LinkDto> GenerateLinks(int? id = null);

        public virtual List<LinkDto> MakeRelSelf(List<LinkDto> links, string rel)
        {
            if (string.IsNullOrEmpty(rel) || links == null || links.Count == 0)
                return links;

            var link = links.FirstOrDefault(l =>
                l.Rel.Equals(rel, StringComparison.OrdinalIgnoreCase)
                || l.Rel.Contains(rel, StringComparison.OrdinalIgnoreCase)
            );

            if (link == null)
                return links;

            links.Remove(link);
            links.Add(new LinkDto(link.Href, "self", link.Method));

            return links;
        }

        protected string? GetUriByAction(string actionName, object? values = null)
        {
            try
            {
                Console.WriteLine($"Generating URI - Action: {actionName}, Controller: {ControllerName}");
                if (values != null)
                {
                    Console.WriteLine($"Values: {string.Join(", ", values.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(values)}"))}");
                }
                
                var uri = _generator.GetUriByAction(
                    httpContext: _context.HttpContext,
                    action: actionName,
                    controller: ControllerName,
                    values: values
                );
                
                Console.WriteLine($"Generated URI: {uri}");
                
                // Debug logging
                if (string.IsNullOrEmpty(uri))
                {
                    Console.WriteLine($"Failed to generate URI for Action: {actionName}, Controller: {ControllerName}");
                }
                
                return uri;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating URI for Action: {actionName}, Controller: {ControllerName}, Error: {ex.Message}");
                return null;
            }
        }
    }
}
