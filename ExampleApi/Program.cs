using MongoDB.Driver;
using static ExampleApi.Controllers.ThingerController;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IMongoClient>(sp => new MongoClient(sp.GetService<MongoClientSettings>()));
builder.Services.AddScoped<IMongoDatabase>(sp => sp.GetService<IMongoClient>().GetDatabase("TestDatabase"));
builder.Services.AddScoped<IMongoCollection<Thinger>>(sp => sp.GetService<IMongoDatabase>().GetCollection<Thinger>("Thingers"));

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
