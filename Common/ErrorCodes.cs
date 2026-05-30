namespace ProjetoLP.API.Common;

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
    public const string InvalidPassword    = "INVALID_PASSWORD";
    public const string InvalidFileType    = "INVALID_FILE_TYPE";
    public const string FileTooLarge       = "FILE_TOO_LARGE";
}
