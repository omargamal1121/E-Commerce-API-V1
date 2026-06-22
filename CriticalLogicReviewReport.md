# Critical Logic Review Report

Scope: payment, orders, cart, discounts, checkout, and inventory-sensitive flows.

## Critical Findings

### DeliveryCompany Can Perform Admin-Only Order Transitions

`PresentationLayer/Controllers/BaseController.cs` treats `DeliveryCompany` as a management role. `PresentationLayer/Controllers/OrderController.cs` then allows management roles to confirm, cancel, refund, return, expire, complete, ship, and deliver orders.

Risk: delivery users can perform financial/admin actions that should be restricted.

Recommendation: remove `DeliveryCompany` from `HasManagementRole`; add explicit delivery-only authorization for shipping/delivery transitions.

### Payment Row Locking Is Broken

`InfrastructureLayer/Repository/PaymentRepository.cs` locks the `Orders` table using a payment id instead of locking the `Payments` table.

Risk: concurrent webhooks/status jobs can update the same payment without proper serialization.

Recommendation: change the lock SQL to lock `Payments` by payment id.

### Payment Completion And Order Confirmation Are Not Atomic

`ApplicationLayer/Services/PaymentWebhookService/PaymentWebhookService.cs` updates payment status and order status through separate service calls and separate transactions.

Risk: payment can be marked completed while the order remains pending if the order update fails.

Recommendation: process webhook persistence, payment update, and order update in one transaction, or use a durable outbox/retry state.

### Webhook Idempotency Can Permanently Skip Recovery

The webhook record is committed before payment/order updates finish. Duplicate webhooks are later skipped by `WebhookUniqueKey`.

Risk: if the first attempt logs the webhook but fails to update payment/order, retries are ignored.

Recommendation: mark a webhook as processed only after payment/order updates succeed, or store failed processing state and retry.

### Cart Accepts Negative Or Zero Quantity On Add

`ApplicationLayer/DtoModels/CartDtos/CreateCartItemDto.cs` does not require positive quantity, and `CartCommandService.AddItemToCartAsync` only checks requested quantity against stock.

Risk: negative cart quantities can create negative totals or reduce existing item quantity incorrectly.

Recommendation: add `[Range(1, int.MaxValue)]` on quantity and enforce `itemDto.Quantity > 0` in service logic.

### Duplicate Cart Items Are Possible Under Concurrency

The cart is loaded before acquiring the cart lock, and duplicate checks use the pre-lock snapshot. The `(CartId, ProductId, ProductVariantId)` index is not unique.

Risk: concurrent add requests can insert duplicate cart rows for the same variant.

Recommendation: add a unique database constraint and re-query cart items after acquiring the lock.

## High Findings

### Failed PayMob Payment Does Not Expire Or Release The Order Immediately

Failed webhook transactions set payment status to failed, but order status defaults to `PendingPayment`.

Risk: stock remains reserved until a later background expiry.

Recommendation: map failed terminal payment webhooks to `PaymentExpired` or a dedicated failed-payment order state and restock intentionally.

### Cash On Delivery Confirmation Is Queued Before Payment Creation Commits

COD order confirmation is enqueued before the payment row is created and before the transaction commits.

Risk: an order can be confirmed even if payment creation later rolls back.

Recommendation: enqueue confirmation only after commit, or update COD payment/order state in the same transaction.

### Checkout Invalidation Is Asynchronous

Cart mutations enqueue checkout removal in background jobs instead of clearing `CheckoutDate` in the same transaction.

Risk: users can checkout, modify the cart, and create an order before checkout state is cleared.

Recommendation: clear `CheckoutDate` synchronously inside the same transaction for cart mutations and price/discount changes.

### Cart Locking Silently Fails

`CartRepository.LockCartForUpdateAsnyc` catches lock errors and only logs them.

Risk: callers continue as if locking succeeded, weakening concurrency guarantees.

Recommendation: let lock failures throw or return a failure result that aborts the operation.

### Discount End Date Update Logic Is Wrong

`DiscountCommandService.UpdateDiscountAsync` only updates `EndDate` when the existing end date is already in the past.

Risk: admins cannot correct or extend active/upcoming discounts.

Recommendation: update `EndDate` whenever a new valid value is supplied, then validate `StartDate < EndDate`.

## Medium Findings

### Payment Uniqueness Conflicts With Retry Behavior

`AppDbContext` enforces unique `(OrderId, PaymentMethodId)`, while the service logic expects retries and old pending payments to become failed.

Risk: customers may be blocked from retrying the same payment method after failure/expiry.

Recommendation: use a uniqueness model that matches the intended retry flow, such as active-pending uniqueness or including provider attempt identity.

### Product Discount Activation Logic Is Inconsistent

`ProductDiscountService.ApplyDiscountToProductsAsync` treats a discount as active if `IsActive` is true or the date window is valid. Other pricing paths require both `IsActive` and valid dates.

Risk: cart stored prices can be updated differently from displayed/current prices.

Recommendation: standardize active discount logic to: `IsActive && StartDate <= now && EndDate > now && DeletedAt == null`.

### Checkout Endpoint Does Not Validate Cart Contents

`CartCommandService.UpdateCheckoutData` sets `CheckoutDate` without locking the cart or validating active items, variants, stock, and current prices.

Risk: checkout state can be misleading or stale.

Recommendation: validate cart contents and totals during checkout, or make order creation the only authoritative checkout validation point and remove checkout timestamp dependency.

## Suggested Fix Order

1. Fix authorization boundaries for `DeliveryCompany`.
2. Fix payment lock SQL and atomic webhook processing.
3. Fix cart quantity validation and unique cart item constraint.
4. Make checkout invalidation synchronous.
5. Standardize discount active-state logic.
6. Rework payment retry uniqueness to match business rules.
