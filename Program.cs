using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.MapPost("/contact/submit", async (IConfiguration config, ContactRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { success = false, message = "Please fill all fields correctly." });

    var smtpHost = config["Smtp:Host"];
    var smtpPort = int.TryParse(config["Smtp:Port"], out var port) ? port : 587;
    var smtpUser = config["Smtp:Username"];
    var smtpPass = config["Smtp:Password"];
    var toAddr = config["Smtp:To"] ?? config["ContactEmail"];
    var useSsl = bool.TryParse(config["Smtp:EnableSsl"], out var ssl) && ssl;

    try
    {
        if (!string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser))
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(req.Name, req.Email));
            mimeMessage.To.Add(new MailboxAddress("SaSilva Tech Advisors", toAddr));
            mimeMessage.Subject = $"New Contact Inquiry from {req.Name}";

            var emailBuilder = new BodyBuilder();
            var messageHtml = $"<h2>New Contact Form Submission</h2>" +
                $"<p><strong>Name:</strong> {WebUtility.HtmlEncode(req.Name)}</p>" +
                $"<p><strong>Email:</strong> {WebUtility.HtmlEncode(req.Email)}</p>" +
                $"<p><strong>Message:</strong></p>" +
                $"<p>{WebUtility.HtmlEncode(req.Message)}</p>";
            emailBuilder.HtmlBody = messageHtml;
            mimeMessage.Body = emailBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            if (useSsl)
                await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            else
                await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.None);

            await smtp.AuthenticateAsync(smtpUser, smtpPass);
            await smtp.SendAsync(mimeMessage);
            await smtp.DisconnectAsync(true);

            Console.WriteLine($"Contact email sent from {req.Email} via {smtpHost}:{smtpPort}");
        }
        else
        {
            Console.WriteLine($"[SMTP not configured] Contact form: {req.Name} ({req.Email}) - {req.Message}");
        }

        return Results.Ok(new { success = true, message = "Thank you for reaching out. We'll be in touch shortly." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send contact email: {ex.Message}");
        return Results.Ok(new { success = true, message = "Thank you for reaching out. We'll be in touch shortly." });
    }
});

app.Run();

public record ContactRequest(string Name, string Email, string Message);
