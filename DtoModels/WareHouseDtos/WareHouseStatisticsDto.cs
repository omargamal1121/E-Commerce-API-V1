namespace E_Commerce.DtoModels.WareHouseDtos
{
    public class WareHouseStatisticsDto
    {
        public int TotalProducts { get; set; }
        public int TotalInventoryItems { get; set; }
        public double CapacityUtilization { get; set; }
        public int ActiveTransfers { get; set; }
        public int PendingTransfers { get; set; }
        public DateTime LastUpdated { get; set; }
    }
} 