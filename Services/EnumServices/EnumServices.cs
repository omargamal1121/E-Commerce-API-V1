using E_Commerce.DtoModels.EnumDtos;

namespace E_Commerce.Services.EnumServices
{
	public class EnumServices
	{
		public static List<EnumDto> ToSelectList<T>() where T : Enum
		{
			return Enum.GetValues(typeof(T))
					   .Cast<T>()
					   .Select(e => new EnumDto
					   {
						   id = Convert.ToInt32(e),
						   name = e.ToString()
					   })
					   .ToList<EnumDto>();
		}

	}
	
}
