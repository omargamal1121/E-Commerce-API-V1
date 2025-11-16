using ApplicationLayer.DtoModels.PaymentDtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApplicationLayer.DtoModels.PaymentDtos
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

        [JsonProperty("hmac")]
        public string? Hmac { get; set; }
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

        // ? Added missing HMAC fields
        [JsonProperty("created_at")]
        public string? CreatedAt { get; set; }

        [JsonProperty("error_occured")]
        public bool ErrorOccured { get; set; }

        [JsonProperty("has_parent_transaction")]
        public bool HasParentTransaction { get; set; }

        [JsonProperty("integration_id")]
        public long? IntegrationId { get; set; }

        [JsonProperty("is_3d_secure")]
        public bool Is3DSecure { get; set; }

        [JsonProperty("is_auth")]
        public bool IsAuth { get; set; }

        [JsonProperty("is_capture")]
        public bool IsCapture { get; set; }

        [JsonProperty("is_refunded")]
        public bool IsRefunded { get; set; }

        [JsonProperty("is_standalone_payment")]
        public bool IsStandalonePayment { get; set; }

        [JsonProperty("is_voided")]
        public bool IsVoided { get; set; }

        [JsonProperty("owner")]
        public long? Owner { get; set; }

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

        // ? Fixed: HMAC expects 'pan' field, but we'll keep both for compatibility
        [JsonProperty("pan")]
        public string? Pan { get; set; }

        // Keep this for display purposes (last 4 digits)
        [JsonIgnore]
        public string? PanLast4 => Pan?.Length > 4 ? Pan.Substring(Pan.Length - 4) : Pan;
    }
}

// ? Alternative: More flexible DTO that captures all fields
public class PaymobTransactionObjFlexible
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

    // ? Use JsonExtensionData to capture any additional fields
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }

    // Helper methods to get HMAC fields safely
    public string? GetCreatedAt() => AdditionalData?.GetValueOrDefault("created_at")?.ToString();
    public bool GetErrorOccured() => AdditionalData?.GetValueOrDefault("error_occured")?.Value<bool>() ?? false;
    public bool GetHasParentTransaction() => AdditionalData?.GetValueOrDefault("has_parent_transaction")?.Value<bool>() ?? false;
    public long? GetIntegrationId() => AdditionalData?.GetValueOrDefault("integration_id")?.Value<long>();
    public bool GetIs3DSecure() => AdditionalData?.GetValueOrDefault("is_3d_secure")?.Value<bool>() ?? false;
    public bool GetIsAuth() => AdditionalData?.GetValueOrDefault("is_auth")?.Value<bool>() ?? false;
    public bool GetIsCapture() => AdditionalData?.GetValueOrDefault("is_capture")?.Value<bool>() ?? false;
    public bool GetIsRefunded() => AdditionalData?.GetValueOrDefault("is_refunded")?.Value<bool>() ?? false;
    public bool GetIsStandalonePayment() => AdditionalData?.GetValueOrDefault("is_standalone_payment")?.Value<bool>() ?? false;
    public bool GetIsVoided() => AdditionalData?.GetValueOrDefault("is_voided")?.Value<bool>() ?? false;
    public long? GetOwner() => AdditionalData?.GetValueOrDefault("owner")?.Value<long>();
}

