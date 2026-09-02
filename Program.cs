using HusnaFactory.Data;
using HusnaFactory.Services;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// --- DATABASE & SSL CONFIGURATION ---
var caCert = Environment.GetEnvironmentVariable("CA_CERT");
if (!string.IsNullOrEmpty(caCert))
{
    string tempCertPath = Path.Combine(Path.GetTempPath(), "ca-certificate.crt");
    File.WriteAllText(tempCertPath, caCert);

    foreach (var key in new[] { "MalirConnection", "NewBranchConnection", "FactoryHubConnection" })
    {
        var cs = builder.Configuration.GetConnectionString(key);
        if (!string.IsNullOrEmpty(cs))
        {
            if (!cs.EndsWith(";")) cs += ";";
            cs += $"SslCa={tempCertPath};SslMode=Required;";
            builder.Configuration[$"ConnectionStrings:{key}"] = cs;
        }
    }
}

builder.Services.AddSingleton<FactoryDbConnections>();
// --- END DATABASE CONFIGURATION ---

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 15 * 1024 * 1024; // 15 MB — measurement scan previews
    });

builder.Services.AddScoped<FactoryAuthService>();
builder.Services.AddScoped<FactoryOrderService>();
builder.Services.AddScoped<FactoryMeasurementService>();
builder.Services.AddScoped<FactoryLookupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
