using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.CategoryDtos
{
    public class PatchCategoryDto
    {
        /// <summary>
        /// Set to true to activate, false to deactivate, null to not change
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Set to false to restore (undelete), null to not change
        /// </summary>
        public bool? IsDeleted { get; set; }

        /// <summary>
        /// Update category name
        /// </summary>
        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }

        /// <summary>
        /// Update category description
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Update display order
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? DisplayOrder { get; set; }
    }
}


