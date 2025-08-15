using Newtonsoft.Json;

namespace E_Commerce.DtoModels.PaymentDtos
{
    public class PaymobWebhookDto
    {
        [JsonProperty("type")] 
        public string? Type { get; set; }
        [JsonProperty("obj")] 
        public PaymobTransactionObj? Obj { get; set; }
        [JsonProperty("issuer_bank")] 
        public string? IssuerBank { get; set; }
        [JsonProperty("transaction_processed_callback_responses")] 
        public string? TransactionProcessedCallbackResponses { get; set; }
    }

    public class PaymobTransactionObj
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("pending")]
        public bool Pending { get; set; }
        [JsonProperty("amount_cents")]
        public long AmountCents { get; set; }
        [JsonProperty("success")] 
        public bool Success { get; set; }
        [JsonProperty("currency")]
        public string? Currency { get; set; }
        [JsonProperty("order")] 
        public PaymobOrder? Order { get; set; }
        [JsonProperty("payment_key_claims")] 
        public PaymobPaymentKeyClaims? PaymentKeyClaims { get; set; }
        [JsonProperty("source_data")] 
        public PaymobSourceData? SourceData { get; set; }
    }

    public class PaymobOrder
    {
        [JsonProperty("id")] 
        public long Id { get; set; }
        [JsonProperty("merchant_order_id")] 
        public string? MerchantOrderId { get; set; }
        [JsonProperty("amount_cents")]
        public long AmountCents { get; set; }
        [JsonProperty("currency")] 
        public string? Currency { get; set; }
        [JsonProperty("paid_amount_cents")] 
        public long PaidAmountCents { get; set; }
        [JsonProperty("payment_status")] 
        public string? PaymentStatus { get; set; }
    }

    public class PaymobPaymentKeyClaims
    {
        [JsonProperty("order_id")]
        public long OrderId { get; set; }
        [JsonProperty("amount_cents")] 
        public long AmountCents { get; set; }
        [JsonProperty("currency")] 
        public string? Currency { get; set; }
        [JsonProperty("integration_id")]
        public long IntegrationId { get; set; }
        [JsonProperty("user_id")]
        public long UserId { get; set; }
    }

    public class PaymobSourceData
    {
        [JsonProperty("type")] 
        public string? Type { get; set; }
        [JsonProperty("sub_type")]
        public string? SubType { get; set; }
        [JsonProperty("pan")]
        public string? PanLast4 { get; set; }
    }
}
