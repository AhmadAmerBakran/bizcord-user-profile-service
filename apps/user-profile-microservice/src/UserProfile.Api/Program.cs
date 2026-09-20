using UserProfile.Api.Configuration;
using UserProfile.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();
builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddMessageHandlers(typeof(MessageClientServiceCollectionExtensions).Assembly);
builder.Services.AddUserProfiles();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
