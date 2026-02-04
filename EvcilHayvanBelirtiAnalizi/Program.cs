using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. ADIM: Sisteme Giriþ Servislerini Ekle (Bu kýsým butonlarý canlandýrýr)
builder.Services.AddControllersWithViews();

// Çerez (Cookie) tabanlý giriþ sistemini mühürlüyoruz
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";     // Giriþ sayfasý nerede?
        options.LogoutPath = "/Account/Logout";   // Çýkýþ komutu nereye gider?
        options.AccessDeniedPath = "/Home/Index"; // Yetkisiz giriþte nereye atsýn?
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 2. ADIM: Sýralama Çok Önemli (Bu iki satýr yer deðiþtirirse giriþ yapamazsýn)
app.UseAuthentication(); // Önce: Sen kimsin? (Login kontrolü)
app.UseAuthorization();  // Sonra: Yetkin ne? (Veteriner/Admin kontrolü)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();