using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Identity.Domain;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class IdentityErrors
{
    public static readonly Error InvalidEmail = Error.Validation("identity.invalid_email", "E-posta adresi geçersiz.");
    public static readonly Error Underage = Error.Validation("identity.underage", "Kayıt için en az 13 yaşında olmalısın.");
    public static readonly Error MarketingConsentNotAllowed = Error.Validation("identity.marketing_consent_not_allowed", "Ticari ileti izni yalnızca 18 yaş ve üzerine sorulur.");
    public static readonly Error OutdatedTerms = Error.Conflict("identity.outdated_terms", "Koşullar veya aydınlatma metni güncellendi; lütfen güncel metni onayla.");
    public static readonly Error InvalidCredentials = Error.Unauthorized("identity.invalid_credentials", "E-posta veya şifre hatalı.");
    public static readonly Error EmailNotVerified = Error.Forbidden("identity.email_not_verified", "E-posta adresin henüz doğrulanmadı.");
    public static readonly Error DeletionPending = Error.Forbidden("identity.deletion_pending", "Hesabın silinmek üzere; geri almak ister misin?");
    public static readonly Error NotPendingDeletion = Error.Conflict("identity.not_pending_deletion", "Hesap silinmek üzere değil.");
    public static readonly Error AlreadyVerified = Error.Conflict("identity.already_verified", "E-posta zaten doğrulanmış.");
    public static readonly Error InvalidCode = Error.Validation("identity.invalid_code", "Kod hatalı.");
    public static readonly Error CodeExpired = Error.Validation("identity.code_expired", "Kodun süresi doldu; yeni kod iste.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("identity.invalid_refresh_token", "Oturum geçersiz; tekrar giriş yap.");
    public static readonly Error InvalidPassword = Error.Validation("identity.invalid_password", "Şifre hatalı.");
    public static readonly Error InvalidMfaToken = Error.Unauthorized("identity.invalid_mfa_token", "Doğrulama oturumu geçersiz; tekrar giriş yap.");
    public static readonly Error InvalidTotpCode = Error.Validation("identity.invalid_totp_code", "Doğrulama kodu hatalı.");
    public static readonly Error TotpAlreadyEnabled = Error.Conflict("identity.totp_already_enabled", "İki adımlı doğrulama zaten kurulu.");
    public static readonly Error TotpSetupNotStarted = Error.Conflict("identity.totp_setup_not_started", "İki adımlı doğrulama kurulumu başlatılmadı.");
    public static readonly Error UserNotFound = Error.NotFound("identity.user_not_found", "Kullanıcı bulunamadı.");
}
