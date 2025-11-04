using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default")!;
    opt.UseSqlite(cs);
});
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();

// Polly v8: safe retries for idempotent outbound calls
builder.Services.AddHttpClient("PaymentsGateway")
    .AddResilienceHandler("retry", rb =>
        rb.AddRetry(new()
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential
        })
        .AddTimeout(TimeSpan.FromSeconds(10)));

var app = builder.Build();

// Apply migrations at startup (demo purposes)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}


// Simple health
app.MapGet("/", () => Results.Text("Idempotent API is running", MediaTypeNames.Text.Plain));

// Idempotent POST /payments
app.MapPost("/payments", async Task<Results<Ok<PaymentResponse>, BadRequest<string>, Conflict<string>, ProblemHttpResult>>
    (PaymentRequest request,
     HttpContext http,
     AppDbContext db,
     IIdempotencyStore idemStore) =>
{
    // 1) Require Idempotency-Key
    if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var keyVals) ||
        string.IsNullOrWhiteSpace(keyVals.ToString()))
    {
        return TypedResults.BadRequest("Missing Idempotency-Key header");
    }
    var key = keyVals.ToString().Trim();

    // 2) Compute request fingerprint
    var reqHash = await idemStore.ComputeHashAsync(request);

    // 3) If we’ve seen this key before, ensure payload matches; if yes, return stored response
    var existingHash = await idemStore.GetRequestHashAsync(key);
    if (existingHash is not null)
    {
        if (!string.Equals(existingHash, reqHash, StringComparison.Ordinal))
        {
            // Same key, different payload -> client bug / conflict
            return TypedResults.Conflict("Idempotency-Key already used with a different payload.");
        }

        var hit = await idemStore.TryGetAsync(key);
        if (hit is { found: true } && hit.Value.found)
        {
            // Replay stored response
            http.Response.StatusCode = hit.Value.status;
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync(hit.Value.body);
            return default!; // response already written
        }
        // If record exists but expired from cache, we’ll fall through and re-save (defensive)
    }

    // 4) Process the operation (this side-effect happens ONCE per unique key+payload)
    //    For demo: insert a Payment row
    var payment = new Payment
    {
        Amount = request.Amount,
        Currency = request.Currency,
        Recipient = request.Recipient,
        IdempotencyKey = key,
        Status = PaymentStatus.Authorized
    };

    try
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
    {
        // Unique constraint on IdempotencyKey triggered
        // Load and return the existing payment (replay equivalent)
        var existing = await db.Payments.AsNoTracking().FirstAsync(p => p.IdempotencyKey == key);
        var replay = new PaymentResponse(existing.Id, existing.Amount, existing.Currency, existing.Recipient, existing.Status.ToString(), existing.CreatedAtUtc);
        // Save to idempotency store if not present
        await idemStore.SaveAsync(key, replay, StatusCodes.Status200OK, reqHash, TimeSpan.FromMinutes(5));
        return TypedResults.Ok(replay);
    }

    var response = new PaymentResponse(payment.Id, payment.Amount, payment.Currency, payment.Recipient, payment.Status.ToString(), payment.CreatedAtUtc);

    // 5) Persist the response for future replays
    var ttl = TimeSpan.FromMinutes(5);
    await idemStore.SaveAsync(key, response, StatusCodes.Status200OK, reqHash, ttl);

    return TypedResults.Ok(response);
})
.WithName("CreatePayment")
.Produces<PaymentResponse>(StatusCodes.Status200OK)
.Produces<string>(StatusCodes.Status409Conflict)
.Produces<string>(StatusCodes.Status400BadRequest);

// Idempotent GET/PUT/DELETE examples (PUT is idempotent by nature)
app.MapPut("/payments/{id:guid}/capture", async Task<Results<Ok<PaymentResponse>, NotFound>>
    (Guid id, AppDbContext db) =>
{
    var p = await db.Payments.FindAsync(id);
    if (p is null) return TypedResults.NotFound();

    p.Status = PaymentStatus.Captured;
    await db.SaveChangesAsync();

    var res = new PaymentResponse(p.Id, p.Amount, p.Currency, p.Recipient, p.Status.ToString(), p.CreatedAtUtc);
    return TypedResults.Ok(res);
});


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
