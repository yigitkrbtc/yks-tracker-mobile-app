using System.Reflection;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Application;

/// <summary>Plain-text mails. The product name comes from Directory.Build.props (single rename point).</summary>
internal static class IdentityEmails
{
    private static readonly string Product =
        typeof(IdentityEmails).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Nirengi";

    public static EmailMessage VerificationCode(string to, string firstName, string code, TimeSpan lifetime) => new(
        to,
        $"{Product} doğrulama kodun: {code}",
        $"Merhaba {firstName},\n\n{Product} hesabını doğrulamak için kodun: {code}\n\nKod {lifetime.TotalMinutes:0} dakika geçerli. Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin.");

    public static EmailMessage PasswordResetCode(string to, string firstName, string code, TimeSpan lifetime) => new(
        to,
        $"{Product} şifre sıfırlama kodun: {code}",
        $"Merhaba {firstName},\n\nŞifreni sıfırlamak için kodun: {code}\n\nKod {lifetime.TotalMinutes:0} dakika geçerli. Bu isteği sen yapmadıysan şifren değişmez; bu e-postayı yok sayabilirsin.");

    public static EmailMessage AccountExists(string to, string firstName) => new(
        to,
        $"{Product} hesabın zaten var",
        $"Merhaba {firstName},\n\nBu e-posta adresiyle yeniden kayıt denendi; hesabın zaten var. Şifreni hatırlamıyorsan uygulamada \"Şifremi unuttum\" adımını kullan.");

    public static EmailMessage DeletionScheduled(string to, string firstName, DateTimeOffset purgeAt) => new(
        to,
        $"{Product} hesabın silinecek",
        $"Merhaba {firstName},\n\nHesabın ve tüm verilerin {purgeAt.ToOffset(TimeSpan.FromHours(3)):dd.MM.yyyy} tarihinde kalıcı olarak silinecek. Vazgeçersen bu tarihe kadar uygulamaya giriş yapıp \"Hesabı geri al\" seçeneğini kullanabilirsin.");
}
