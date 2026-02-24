using JaiDee.Application;
using JaiDee.API.Line;
using JaiDee.API.Middleware;
using JaiDee.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<LineBotOptions>(builder.Configuration.GetSection(LineBotOptions.SectionName));
builder.Services.AddHttpClient<ILineMessagingClient, LineMessagingClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LineBotOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
