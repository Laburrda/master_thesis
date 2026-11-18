using Shop.Infrastructure;
using Shop.Application;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Contracts.Errors;
using Shop.Application.Common;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problems = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => $"{entry.Key}: {entry.Value?.Errors[0].ErrorMessage}");


            var message = string.Join("; ", problems);
            return new BadRequestObjectResult(
                new ErrorResponse(new ErrorDetails(ErrorCodes.ValidationError, message)));
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Shop API v1");
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();


