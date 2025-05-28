using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Chamsoc.Filters;
using Chamsoc.Hubs;
using Chamsoc.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Thêm dịch vụ
builder.Services.AddControllersWithViews();

// Cấu hình Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7198",
                " https://fb08-183-80-111-9.ngrok-free.app") // Thêm origin của ứng dụng web
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Cấu hình DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Cấu hình Identity options
builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
});

// Thêm global filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new CustomAuthorizationFilter());
});
builder.Services.AddHttpClient<OpenRouterService>();
builder.Services.AddHttpClient<OpenAIService>();
// Thêm SignalR
builder.Services.AddSignalR();

// Thêm logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

var app = builder.Build();

// Tạo tài khoản Admin và vai trò
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
        if (!await roleManager.RoleExistsAsync("Senior"))
        {
            await roleManager.CreateAsync(new IdentityRole("Senior"));
        }
        if (!await roleManager.RoleExistsAsync("Caregiver"))
        {
            await roleManager.CreateAsync(new IdentityRole("Caregiver"));
        }

        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@gkmcare.com",
                PhoneNumber = "0123456789",
                Role = "Admin",
                RoleId = "0",
                IsLocked = false,
                Balance = 0
            };
            var result = await userManager.CreateAsync(adminUser, "Admin123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
            else
            {
                throw new Exception("Không thể tạo tài khoản Admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Lỗi khi tạo tài khoản Admin");
    }
}

// Cấu hình pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = "/uploads"
});
app.UseRouting();

// Áp dụng CORS trước authentication và authorization
app.UseCors("AllowSpecificOrigins"); // Chỉ dùng chính sách đã định nghĩa

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<CallHub>("/callHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=About}/{id?}");

app.Logger.LogInformation("Server khởi động. Lắng nghe tại https://localhost:7198");

app.Run();