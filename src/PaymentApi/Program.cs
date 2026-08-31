using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PaymentApi;

var builder = WebApplication.CreateBuilder(args);
var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PaymentQueryService>();
builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
builder.Services.AddHttpClient("callbacks", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = error is BadHttpRequestException ? 400 : 500;
    await context.Response.WriteAsJsonAsync(new ErrorResponse(
        error is BadHttpRequestException ? "Invalid request" : "Internal server error"));
}));
app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();
app.Run();

public partial class Program { }
