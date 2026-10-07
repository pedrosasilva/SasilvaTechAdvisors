using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

var config = app.Configuration;
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ContactForm");

app.MapPost("/contact/submit", async (HttpRequest request) =>
{
    ContactRequest? req;
    try
    {
        req = await request.ReadFromJsonAsync<ContactRequest>();
    }
    catch
    {
        req = null;
    }

    if (req is null
        || string.IsNullOrWhiteSpace(req.Name)
        || string.IsNullOrWhiteSpace(req.Email)
        || string.IsNullOrWhiteSpace(req.Message))
    {
        return Results.BadRequest(new { success = false, message = "Please fill all fields correctly." });
    }

    var smtpHost = config["Smtp:Host"];
    var smtpPort = int.TryParse(config["Smtp:Port"], out var port) ? port : 587;
    var smtpUser = config["Smtp:Username"];
    var smtpPass = (config["Smtp:Password"] ?? string.Empty).Trim().Replace(" ", "");
    var adminTo = config["Smtp:AdminEmail"];
    var useSsl = bool.TryParse(config["Smtp:EnableSsl"], out var ssl) && ssl;

    if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser)
        || string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(adminTo))
    {
        logger.LogError(
            "Contact form submitted but SMTP is not fully configured (Host/Username/Password/AdminEmail). Submission: {Name} <{Email}> - {Message}",
            req.Name, req.Email, req.Message);
        return Results.Json(
            new { success = false, message = "We're unable to process your message right now. Please try again shortly or email us directly." },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    // Validated non-null past this point.
    var fromAddr = string.IsNullOrWhiteSpace(config["Smtp:From"]) ? smtpUser : config["Smtp:From"]!;

    try
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress("SaSilva Tech", fromAddr));
        mimeMessage.To.Add(new MailboxAddress("Admin", adminTo));
        mimeMessage.ReplyTo.Add(new MailboxAddress(req.Name, req.Email));
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

        logger.LogInformation("Contact email sent to {Admin} via {Host}:{Port} (visitor: {Visitor})",
            adminTo, smtpHost, smtpPort, req.Email);
        return Results.Ok(new { success = true, message = "Thank you for reaching out. We'll be in touch shortly." });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send contact email (visitor: {Visitor}, sender: {Sender}, admin: {Admin})", req.Email, fromAddr, adminTo);
        return Results.Json(
            new { success = false, message = "Something went wrong sending your message. Please try again or email us directly." },
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

public record ContactRequest(string Name, string Email, string Message);
