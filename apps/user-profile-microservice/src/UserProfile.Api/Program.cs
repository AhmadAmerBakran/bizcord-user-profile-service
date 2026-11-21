using UserProfile.Api.Configuration;
using UserProfile.Api.Messaging;
using UserProfile.Api.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// Application logs stay on stdout. Fluentd handles their delivery outside this process.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
await VaultConfiguration.LoadAsync(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProfileSecurity(builder.Configuration);
builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddMessageHandlers(typeof(MessageClientServiceCollectionExtensions).Assembly);
builder.Services.AddUserProfiles();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<ProfileQuery>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGraphQL("/graphql").RequireAuthorization("ProfileRead");

app.Logger.LogInformation("User Profile API logging is handled by stdout.");
app.Run();

public partial class Program;
