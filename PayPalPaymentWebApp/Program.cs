using Microsoft.EntityFrameworkCore;
using PayPalPaymentWebApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PaypalPatyment")));

// Configure PayPal service with dependency injection
builder.Services.AddScoped<PayPalService>(sp => new PayPalService(
    clientId: builder.Configuration["PayPal:ClientId"],
    apiUrl: builder.Configuration["PayPal:ApiUrl"],  // Corrected this line
    secret: builder.Configuration["PayPal:Secret"]));


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
