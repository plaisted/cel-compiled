using System.Text.Json;
using System.Text.Json.Serialization;
using Cel.Compiled;
using Cel.Compiled.Compiler;
using Cel.Compiled.Gui;

var builder = WebApplication.CreateBuilder(args);

// Add CORS — allow any localhost port for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy =>
        {
            policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Host == "localhost")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowLocalhost");

app.MapPost("/api/cel/to-gui-model", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var celString = await reader.ReadToEndAsync();

    try
    {
        var node = CelGuiConverter.ToGuiModel(celString);
        return Results.Json(node, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/cel/to-cel-string", async (HttpContext context) =>
{
    try
    {
        var node = await JsonSerializer.DeserializeAsync<CelGuiNode>(context.Request.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (node == null) return Results.BadRequest("Invalid JSON");

        bool pretty = context.Request.Query.TryGetValue("pretty", out var prettyValues) && 
                      bool.TryParse(prettyValues.FirstOrDefault(), out var isPretty) && isPretty;

        var celString = CelGuiConverter.ToCelString(node, pretty);
        return Results.Text(celString);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/cel/validate", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var celString = await reader.ReadToEndAsync();

    try
    {
        CelExpression.Compile<JsonDocument>(celString);
        return Results.Ok(new object[] { });
    }
    catch (CelCompilationException ex)
    {
        var error = new
        {
            message = ex.Message,
            line = ex.Line,
            column = ex.Column,
            position = ex.Position,
            length = ex.SourceSpan is { } span ? span.End - span.Start : (int?)null,
            severity = "error"
        };
        return Results.Ok(new[] { error });
    }
    catch (Exception ex)
    {
        return Results.Ok(new[] { new { message = ex.Message, severity = "error" } });
    }
});

app.MapPost("/api/cel/evaluate", async (HttpContext context) =>
{
    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    JsonElement body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body);
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON body" });
    }

    if (!body.TryGetProperty("expression", out var exprEl) || exprEl.GetString() is not string expression)
        return Results.BadRequest(new { error = "Missing 'expression'" });

    if (!body.TryGetProperty("context", out var contextEl))
        return Results.BadRequest(new { error = "Missing 'context'" });

    try
    {
        var fn = CelExpression.Compile<JsonDocument>(expression);
        using var document = JsonDocument.Parse(contextEl.GetRawText());
        var result = fn(document);

        var typeName = result switch
        {
            null        => "null",
            bool        => "bool",
            long        => "int",
            int         => "int",
            double      => "double",
            float       => "double",
            string      => "string",
            JsonElement => "value",
            _           => result.GetType().Name
        };

        var responseJson = JsonSerializer.Serialize(new { result, type = typeName }, jsonOptions);
        return Results.Content(responseJson, "application/json");
    }
    catch (CelCompilationException ex)
    {
        var error = new
        {
            message = ex.Message,
            line = ex.Line,
            column = ex.Column,
            position = ex.Position,
            length = ex.SourceSpan is { } span ? span.End - span.Start : (int?)null,
            severity = "error"
        };
        return Results.BadRequest(new { errors = new[] { error } });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { errors = new[] { new { message = ex.Message, line = (int?)null, column = (int?)null, position = (int?)null, length = (int?)null, severity = "error" } } });
    }
});

app.Run();
