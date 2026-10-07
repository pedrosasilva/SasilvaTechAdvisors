using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient("formspree", client =>
{
    client.BaseAddress = new Uri("https://formspree.io");
    client.Timeout = TimeSpan.FromSeconds(20);
});

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

    var formId = (config["Formspree:FormId"] ?? string.Empty).Trim();
    var accessKey = (config["Formspree:AccessKey"] ?? string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(formId))
    {
        logger.LogError(
            "Contact form submitted but Formspree is not configured (FormId). Submission: {Name} <{Email}> - {Message}",
            req.Name, req.Email, req.Message);
        return Results.Json(
            new { success = false, message = "We're unable to process your message right now. Please try again shortly or email us directly." },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    try
    {
        var httpFactory = request.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var client = httpFactory.CreateClient("formspree");

        var json = JsonSerializer.Serialize(new { name = req.Name, email = req.Email, message = req.Message });
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/f/{Uri.EscapeDataString(formId)}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(accessKey))
            requestMessage.Headers.Add("Access", accessKey);

        using var response = await client.SendAsync(requestMessage);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Formspree rejected the submission (status {Status}, body: {Body}) (visitor: {Visitor}) - {Message}",
                (int)response.StatusCode, responseBody, req.Email, req.Message);
            return Results.Json(
                new { success = false, message = "Something went wrong sending your message. Please try again or email us directly." },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("Contact form submission forwarded via Formspree form {FormId} (visitor: {Visitor}, name: {Name})",
            formId, req.Email, req.Name);
        return Results.Ok(new { success = true, message = "Thank you for reaching out. We'll be in touch shortly." });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to forward contact form submission to Formspree (visitor: {Visitor})", req.Email);
        return Results.Json(
            new { success = false, message = "Something went wrong sending your message. Please try again or email us directly." },
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

public record ContactRequest(string Name, string Email, string Message);
