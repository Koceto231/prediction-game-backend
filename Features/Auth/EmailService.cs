using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BPFL.API.Features.Auth
{
    public class EmailService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<EmailService> logger;

        public EmailService(IConfiguration _configuration, ILogger<EmailService> _logger)
        {
            configuration = _configuration;
            logger = _logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationUrl, CancellationToken ct = default)
        {
            try
            {
                var fromName = configuration["Email:FromName"];
                var fromAddress = configuration["Email:FromAddress"];
                var smtpHost = configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
                var username = configuration["Email:Username"];
                var password = configuration["Email:Password"];
                var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

                logger.LogInformation(
                    "Email config loaded. FromAddress={FromAddress}, SmtpHost={SmtpHost}, SmtpPort={SmtpPort}, Username={Username}, PasswordExists={PasswordExists}, Environment={Environment}",
                    fromAddress,
                    smtpHost,
                    smtpPort,
                    username,
                    !string.IsNullOrWhiteSpace(password),
                    environment
                );

                if (string.IsNullOrWhiteSpace(fromAddress) ||
                    string.IsNullOrWhiteSpace(smtpHost) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException("Email settings are not configured correctly.");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName ?? "BPFL App", fromAddress));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = "Verify your BPFL account";

                message.Body = new TextPart("html")
                {
                    Text = $@"
                        <div style='font-family:Arial,sans-serif;line-height:1.6'>
                            <h2>Verify your email</h2>
                            <p>Thanks for registering in BPFL Predictor.</p>
                            <p>Click the button below to verify your account:</p>
                            <p>
                                <a href='{verificationUrl}' style='display:inline-block;padding:12px 18px;background:#27c76f;color:#05110a;text-decoration:none;border-radius:8px;font-weight:bold;'>
                                    Verify email
                                </a>
                            </p>
                            <p>If the button does not work, open this link:</p>
                            <p>{verificationUrl}</p>
                        </div>"
                };

                using var smtp = new SmtpClient();

                if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    logger.LogWarning("SMTP certificate validation bypass is ENABLED for Development only.");
                }

                logger.LogInformation("Connecting to SMTP server...");
                await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, ct);

                logger.LogInformation("Authenticating with SMTP server...");
                await smtp.AuthenticateAsync(username, password, ct);

                logger.LogInformation("Sending email to {Email}...", toEmail);
                await smtp.SendAsync(message, ct);

                await smtp.DisconnectAsync(true, ct);

                logger.LogInformation("Verification email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
                throw;
            }
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetUrl, CancellationToken ct = default)
        {
            try
            {

                if (string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Recipient email is missing.", nameof(email));

                email = email.Trim();


                var fromName = configuration["Email:FromName"];
                var fromAddress = configuration["Email:FromAddress"];
                var smtpHost = configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
                var username = configuration["Email:Username"];
                var password = configuration["Email:Password"];
                var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

                if (string.IsNullOrWhiteSpace(fromAddress) ||
                   string.IsNullOrWhiteSpace(smtpHost) ||
                   string.IsNullOrWhiteSpace(username) ||
                   string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException("Email settings are not configured correctly.");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName ?? "BPFL App", fromAddress));
                message.To.Add(MailboxAddress.Parse(email));
                message.Subject = "Reset password";

                message.Body = new TextPart("html")
                {
                    Text = $@"
                <div style='font-family:Arial,sans-serif;line-height:1.6'>
                    <h2>Reset your password</h2>
                    <p>We received a request to reset your password.</p>
                    <p>Click the button below to set a new password:</p>
                    <p>
                        <a href='{resetUrl}' style='display:inline-block;padding:12px 18px;background:#27c76f;color:#05110a;text-decoration:none;border-radius:8px;font-weight:bold;'>
                            Reset password
                        </a>
                    </p>
                    <p>If you did not request this, you can ignore this email.</p>
                    <p>This link expires in 30 minutes.</p>
                </div>"
                };
                using var smtp = new SmtpClient();

                if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                }

                await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, ct);
                await smtp.AuthenticateAsync(username, password, ct);
                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send password reset email to {Email}", email);
                throw;
            }
        }
    }
}
