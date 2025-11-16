
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DtoModels.Shared
{
	public class BaseDto
	{
		public int Id { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

		[CustomValidation(typeof(BaseDto), nameof(ValidateModifiedAt))]

		public DateTime? ModifiedAt { get; set; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		[CustomValidation(typeof(BaseDto), nameof(ValidateDeletedAt))]
		public DateTime? DeletedAt { get; set; }



		public static ValidationResult? ValidateModifiedAt(DateTime? modifiedTime, ValidationContext context)
		{
			var category = context.ObjectInstance as BaseDto;
			if (category == null)
				return new ValidationResult("Invalid object instance.");

			if (modifiedTime.HasValue && modifiedTime <= category.CreatedAt)
				return new ValidationResult($"Modified time must be after created time: {category.CreatedAt:yyyy/MM/dd hh:mm}");

			return ValidationResult.Success;
		}
		public static ValidationResult? ValidateDeletedAt(DateTime? DeletedTime, ValidationContext context)
		{
			var category = context.ObjectInstance as BaseDto;
			if (category == null)
				return new ValidationResult("Invalid object instance.");

			if (DeletedTime.HasValue && DeletedTime <= category.CreatedAt)
				return new ValidationResult($"Modified time must be after created time: {category.CreatedAt:yyyy/MM/dd hh:mm}");

			if (DeletedTime.HasValue && category.ModifiedAt.HasValue && DeletedTime <= category.ModifiedAt)
				return new ValidationResult($"Modified time must be after created time: {category.ModifiedAt:yyyy/MM/dd hh:mm}");

			return ValidationResult.Success;
		}

	}
}


