namespace ApplicationLayer.DtoModels.WareHouseDtos
{
    public class WareHouseCapacityDto
    {
        public int TotalCapacity { get; set; }
        public int UsedCapacity { get; set; }
        public int AvailableCapacity { get; set; }
        public double CapacityPercentage { get; set; }
    }
} 

