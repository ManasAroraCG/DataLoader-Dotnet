using DataLoaderAPI.Repositories;
using DataLoaderAPI.Repositories.Interfaces;
using DataLoaderAPI.Services;
using DataLoaderAPI.Services.Interfaces;
using Azure.Identity;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();
builder.Services.AddScoped<IWorkItemService, WorkItemService>();

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var serviceUri = configuration["DataSource:BlobServiceUri"]
        ?? throw new InvalidOperationException(
            "Missing configuration: DataSource:BlobServiceUri");

    return new BlobServiceClient(
        new Uri(serviceUri),
        new DefaultAzureCredential());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
