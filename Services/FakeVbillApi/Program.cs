using System.Net;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPost("/submit-cdr", (NewCdrRequest request) =>
{
    // 30% chance of failure
    // if (new Random().NextDouble() < 0.3)
    // {
    //     return Results.StatusCode((int)HttpStatusCode.ServiceUnavailable);
    // }

    return Results.Ok(new
    {
        Message = "Cdr received"
    });
});

app.Run();

record NewCdrRequest(string Id, string Data);
