using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using EvcilHayvanBelirtiAnalizi.Services;
using System;

namespace EvcilHayvanBelirtiAnalizi.Controllers
{
    public class AccountController : Controller
    {
        private readonly EmailService _emailService = new EmailService();
        string connStr = "Server=DESKTOP-QI04ERP\\SQLEXPRESS;Database=EvcilHayvanBelirtiAnalizDB;Trusted_Connection=True;TrustServerCertificate=True;";

        [HttpGet] public IActionResult Login() => View();
        [HttpGet] public IActionResult Register() => View();
        [HttpGet] public IActionResult SifremiUnuttum() => View();

        [HttpGet]
        public IActionResult SifreGuncelle(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        public IActionResult SifreGuncelle(string Email, string YeniSifre, string YeniSifreTekrar)
        {
            if (YeniSifre != YeniSifreTekrar)
            {
                ViewBag.Hata = "Girdiğiniz şifreler birbiriyle eşleşmiyor!";
                ViewBag.Email = Email;
                return View();
            }

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = "UPDATE Kullanicilar SET Sifre=@pass WHERE Email=@mail";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@pass", YeniSifre);
                cmd.Parameters.AddWithValue("@mail", Email);
                conn.Open();
                int etkilenenSatir = cmd.ExecuteNonQuery();

                if (etkilenenSatir == 0)
                {
                    ViewBag.Hata = "Kullanıcı bulunamadı, şifre güncellenemedi.";
                    return View();
                }
            }
            TempData["Mesaj"] = "Şifreniz başarıyla güncellendi! Yeni şifrenizle giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> SifremiUnuttum(string Email)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    string query = "SELECT KullaniciAdi FROM Kullanicilar WHERE Email=@mail";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@mail", Email);
                    conn.Open();
                    var user = cmd.ExecuteScalar();

                    if (user != null)
                    {
                        string resetLink = $"{Request.Scheme}://{Request.Host}/Account/SifreGuncelle?email={Email}";
                        string konu = "PetVetPro Şifre Yenileme Bağlantısı";
                        string mesaj = $@"
                            <div style='font-family: Arial; padding: 20px; border: 2px solid #ba2d81; border-radius: 15px;'>
                                <h2 style='color: #ba2d81;'>Merhaba {user},</h2>
                                <p>Şifrenizi sıfırlamak için aşağıdaki butona tıklamanız yeterlidir:</p>
                                <a href='{resetLink}' style='display: inline-block; padding: 12px 25px; background-color: #ba2d81; color: white; text-decoration: none; border-radius: 30px; font-weight: bold;'>ŞİFREMİ GÜNCELLE</a>
                                <p style='margin-top: 20px; font-size: 12px; color: gray;'>Bu talebi siz yapmadıysanız lütfen bu maili görmezden gelin.</p>
                            </div>";

                        await _emailService.SendEmailAsync(Email, konu, mesaj);
                        ViewBag.Mesaj = "Şifre değiştirme linki başarıyla gönderildi! Lütfen Gmail kutunuzu kontrol edin.";
                    }
                    else
                    {
                        ViewBag.Hata = "Bu e-posta adresi sistemde kayıtlı değil!";
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Hata = "Gmail Hatası: " + ex.Message;
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string KullaniciAdi, string Email, string Sifre, string SifreTekrar, string Rol, IFormFile MezuniyetBelgesi)
        {
            if (Sifre != SifreTekrar)
            {
                ViewBag.Hata = "Girdiğiniz şifreler birbiriyle eşleşmiyor!";
                return View();
            }

            string dosyaYolu = "";
            bool onayli = (Rol == "Veteriner") ? false : true;

            if (Rol == "Veteriner" && MezuniyetBelgesi != null && MezuniyetBelgesi.Length > 0)
            {
                var klasorYolu = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "belgeler");
                if (!Directory.Exists(klasorYolu)) Directory.CreateDirectory(klasorYolu);
                var dosyaAdi = Guid.NewGuid().ToString() + Path.GetExtension(MezuniyetBelgesi.FileName);
                var tamYol = Path.Combine(klasorYolu, dosyaAdi);
                using (var stream = new FileStream(tamYol, FileMode.Create)) { await MezuniyetBelgesi.CopyToAsync(stream); }
                dosyaYolu = "/belgeler/" + dosyaAdi;
            }

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = "INSERT INTO Kullanicilar (KullaniciAdi, Email, Sifre, Rol, BelgeYolu, Onayli) VALUES (@ad, @mail, @pass, @rol, @yol, @onay)";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ad", KullaniciAdi);
                cmd.Parameters.AddWithValue("@mail", Email);
                cmd.Parameters.AddWithValue("@pass", Sifre);
                cmd.Parameters.AddWithValue("@rol", Rol);
                cmd.Parameters.AddWithValue("@yol", dosyaYolu);
                cmd.Parameters.AddWithValue("@onay", onayli);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string KullaniciAdi, string Password)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                // Kritik Değişiklik: Veritabanındaki 'Rol' bilgisini çekip Claim olarak işliyoruz
                string query = "SELECT KullaniciAdi, Rol, Onayli FROM Kullanicilar WHERE (KullaniciAdi=@ad OR Email=@ad) AND Sifre=@pass";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ad", KullaniciAdi);
                cmd.Parameters.AddWithValue("@pass", Password);
                conn.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.Read())
                {
                    string rol = dr["Rol"].ToString();
                    bool onayli = Convert.ToBoolean(dr["Onayli"]);

                    // Veteriner onaylanmamışsa girişi engelle
                    if (rol == "Veteriner" && !onayli)
                    {
                        ViewBag.Hata = "Hesabınız henüz yönetici tarafından onaylanmamış!";
                        return View();
                    }

                    // Sisteme rolü 'Admin', 'Veteriner' veya 'Kullanici' olarak tanıtıyoruz
                    var claims = new List<Claim> {
                        new Claim(ClaimTypes.Name, dr["KullaniciAdi"].ToString()),
                        new Claim(ClaimTypes.Role, rol)
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                    // Role göre yönlendirme mühürleri
                    if (rol == "Admin") return RedirectToAction("OnayBekleyenler", "Admin");
                    return rol == "Veteriner" ? RedirectToAction("Bloglar", "Home") : RedirectToAction("Index", "Home");
                }
            }
            ViewBag.Hata = "Bilgiler hatalı veya hesap bulunamadı!";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}