namespace MultiClinica.API.Common;

public static class ErrorCodes
{
    public const string NotFound           = "NOT_FOUND";
    public const string DuplicatePayment   = "DUPLICATE_PAYMENT";
    public const string InactivePatient    = "INACTIVE_PATIENT";
    public const string InvalidFormat      = "INVALID_FORMAT";
    public const string EmptyField         = "EMPTY_FIELD";
    public const string CannotDelete       = "CANNOT_DELETE";
    public const string InvalidValue       = "INVALID_VALUE";
    public const string DuplicateEmail     = "DUPLICATE_EMAIL";
    public const string DuplicateCpf       = "DUPLICATE_CPF";
    public const string DuplicateName      = "DUPLICATE_NAME";
    public const string HasAssociatedRecords = "HAS_ASSOCIATED_RECORDS";
    public const string CannotModify       = "CANNOT_MODIFY";
    public const string LastAdmin          = "LAST_ADMIN";
    public const string InvalidDate        = "INVALID_DATE";
    public const string AppointmentConflict = "APPOINTMENT_CONFLICT";
    public const string InvalidPassword    = "INVALID_PASSWORD";
    public const string InvalidFileType    = "INVALID_FILE_TYPE";
    public const string FileTooLarge       = "FILE_TOO_LARGE";
    public const string TooManyRows        = "TOO_MANY_ROWS";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string Forbidden          = "FORBIDDEN";
    public const string AlreadyOpen        = "ALREADY_OPEN";
    public const string AlreadyLinked      = "ALREADY_LINKED";
    public const string RequestsDisabled   = "RequestsDisabled";
    public const string InvalidStatus      = "INVALID_STATUS";
    public const string NotLinked          = "NOT_LINKED";
    public const string SlotUnavailable    = "SlotUnavailable";
    public const string ProfessionalUnavailable = "ProfessionalUnavailable";
    public const string PlanRequired       = "PLAN_REQUIRED";
    public const string ProfessionalLimit  = "PROFESSIONAL_LIMIT";
    public const string InvalidSignature   = "INVALID_SIGNATURE";
    public const string InvalidToken       = "INVALID_TOKEN";

    // ── Pagamento online (Mercado Pago) ─────────────────────────────────────
    public const string MercadoPagoNotConnected = "MERCADOPAGO_NOT_CONNECTED";
    public const string MercadoPagoPaymentFailed = "MERCADOPAGO_PAYMENT_FAILED";
    public const string MercadoPagoRefundFailed = "MERCADOPAGO_REFUND_FAILED";
    public const string PaymentNotPending    = "PAYMENT_NOT_PENDING";
    public const string PaymentInProgress    = "PAYMENT_IN_PROGRESS";
    public const string InvalidPaymentMethod = "INVALID_PAYMENT_METHOD";
    public const string OnlinePaymentsNotAvailable = "ONLINE_PAYMENTS_NOT_AVAILABLE";
    public const string OnlinePaymentMustBePending = "ONLINE_PAYMENT_MUST_BE_PENDING";
    public const string PaymentMethodNotAllowed = "PAYMENT_METHOD_NOT_ALLOWED";
    public const string InstallmentsNotAllowed = "INSTALLMENTS_NOT_ALLOWED";
    public const string PatientEmailRequired = "PATIENT_EMAIL_REQUIRED";
    public const string PaymentHasPendingOnlinePayment = "PAYMENT_HAS_PENDING_ONLINE_PAYMENT";
    public const string PaymentPaidOnline    = "PAYMENT_PAID_ONLINE";
    public const string PaymentNotRefundable = "PAYMENT_NOT_REFUNDABLE";
}
