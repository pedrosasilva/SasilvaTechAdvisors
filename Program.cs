using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

app.MapPost("/contact/submit", (IConfiguration config, ContactRequest req) => {
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { success = false, message = "Please fill all fields correctly." });

    Console.WriteLine($"Contact form: {req.Name} ({req.Email}) - {req.Message}");
    return Results.Ok(new { success = true, message = "Thank you for reaching out. We'll be in touch shortly." });
});

app.Run();

public record ContactRequest(string Name, string Email, string Message);
