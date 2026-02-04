using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using System;

public class HomeController : Controller
{
    string connectionString = "Server=DESKTOP-QI04ERP\\SQLEXPRESS;Database=EvcilHayvanBelirtiAnalizDB;Trusted_Connection=True;TrustServerCertificate=True;";

    // --- ANA SAYFA ---
    public IActionResult Index()
    {
        var belirtiListesi = new List<object>();
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "SELECT BelirtiId, BelirtiAdi FROM Belirtiler ORDER BY BelirtiAdi ASC";
            SqlCommand cmd = new SqlCommand(query, conn);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader())
            {
                while (dr.Read()) { belirtiListesi.Add(new { Id = dr["BelirtiId"], Ad = dr["BelirtiAdi"].ToString() }); }
            }
        }
        ViewBag.Belirtiler = belirtiListesi;
        return View();
    }

    // --- DETAY SORULARI GETİR ---
    [HttpGet]
    public JsonResult GetBelirtiDetaylari(int belirtiId)
    {
        List<string> detaylar = new List<string>();
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "SELECT SoruMetni FROM BelirtiDetaylari WHERE BelirtiId = @id AND Aktif = 1 ORDER BY SiraNo ASC";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", belirtiId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) { while (dr.Read()) { detaylar.Add(dr["SoruMetni"].ToString()); } }
        }
        return Json(detaylar);
    }

    // --- ANALİZ RAPORU ---
    public IActionResult Liste(int belirtiId, string cevaplar, string petName, string petType, string isNeutered, string age, string gender)
    {
        dynamic sonuc = null;
        string durumKodu = string.Join("_", cevaplar.Split(',').Select(c => c == "Evet" ? "EVT" : "HYR"));
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"SELECT RaporBasligi, TibbiAnaliz, KlinikIslemler, HekimeSorular, KritiklikRenk 
                             FROM RaporIcerikleri WHERE BelirtiId = @bid AND DurumKodu = @dkodu";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@bid", belirtiId);
            cmd.Parameters.AddWithValue("@dkodu", durumKodu);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader())
            {
                if (dr.Read())
                {
                    ViewBag.RaporBasligi = dr["RaporBasligi"].ToString();
                    ViewBag.TibbiAnaliz = dr["TibbiAnaliz"].ToString();
                    ViewBag.Adimlar = dr["KlinikIslemler"].ToString().Split('\n');
                    ViewBag.Sorular = dr["HekimeSorular"].ToString().Split('\n');
                    ViewBag.Renk = dr["KritiklikRenk"].ToString();
                    sonuc = new { PetAdi = petName, PetTuru = petType, Yas = age, Cinsiyet = gender, Kisir = isNeutered };
                }
            }
        }
        if (sonuc == null) return Content("Rapor bulunamadı.");
        return View(sonuc);
    }

    // --- BLOG YAZISI EKLEME ---
    public IActionResult YaziEkle() => View();

    [HttpPost]
    public async Task<IActionResult> YaziEkle(string Baslik, string Icerik, IFormFile Gorsel)
    {
        if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");

        string dosyaAdi = "default-blog.jpg";
        if (Gorsel != null && Gorsel.Length > 0)
        {
            dosyaAdi = Guid.NewGuid().ToString() + Path.GetExtension(Gorsel.FileName);
            string yol = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "blog", dosyaAdi);
            Directory.CreateDirectory(Path.GetDirectoryName(yol));
            using (var stream = new FileStream(yol, FileMode.Create)) { await Gorsel.CopyToAsync(stream); }
        }

        try
        {
            var vId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity.Name;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"INSERT INTO Bloglar (Baslik, Icerik, GorselYolu, YayinTarihi, VeterinerId, BegeniSayisi) 
                                 VALUES (@b, @i, @g, @t, (SELECT KullaniciId FROM Kullanicilar WHERE KullaniciAdi=@v), 0)";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@b", Baslik ?? "Başlıksız");
                cmd.Parameters.AddWithValue("@i", Icerik ?? "");
                cmd.Parameters.AddWithValue("@g", dosyaAdi);
                cmd.Parameters.AddWithValue("@t", DateTime.Now);
                cmd.Parameters.AddWithValue("@v", vId);
                conn.Open(); cmd.ExecuteNonQuery();
            }
            return RedirectToAction("Bloglar");
        }
        catch (Exception ex) { return Content("Hata: " + ex.Message); }
    }

    // --- BLOG LİSTESİ ---
    public IActionResult Bloglar()
    {
        var blogListesi = new List<dynamic>();
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "SELECT B.*, K.KullaniciAdi FROM Bloglar B INNER JOIN Kullanicilar K ON B.VeterinerId = K.KullaniciId ORDER BY B.YayinTarihi DESC";
            SqlCommand cmd = new SqlCommand(query, conn);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    blogListesi.Add(new
                    {
                        Id = dr["Id"],
                        Baslik = dr["Baslik"].ToString(),
                        Icerik = dr["Icerik"].ToString(),
                        Tarih = Convert.ToDateTime(dr["YayinTarihi"]),
                        Yazar = dr["KullaniciAdi"].ToString(),
                        GorselYolu = dr["GorselYolu"] != DBNull.Value ? dr["GorselYolu"].ToString() : "default-blog.jpg",
                        BegeniSayisi = dr["BegeniSayisi"] != DBNull.Value ? dr["BegeniSayisi"] : 0
                    });
                }
            }
        }
        return View(blogListesi);
    }

    // --- BLOG DETAY VE YORUMLAR ---
    public IActionResult Bloglar_detay(int id)
    {
        dynamic blog = null;
        var yorumListesi = new List<dynamic>();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            string query = @"SELECT B.*, K.KullaniciAdi FROM Bloglar B 
                             INNER JOIN Kullanicilar K ON B.VeterinerId = K.KullaniciId 
                             WHERE B.Id = @id";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using (SqlDataReader dr = cmd.ExecuteReader())
            {
                if (dr.Read())
                {
                    blog = new
                    {
                        Id = dr["Id"],
                        Baslik = dr["Baslik"].ToString(),
                        Icerik = dr["Icerik"].ToString(),
                        Tarih = Convert.ToDateTime(dr["YayinTarihi"]),
                        Yazar = dr["KullaniciAdi"].ToString(),
                        GorselYolu = dr["GorselYolu"] != DBNull.Value ? dr["GorselYolu"].ToString() : "default-blog.jpg",
                        BegeniSayisi = dr["BegeniSayisi"] != DBNull.Value ? dr["BegeniSayisi"] : 0
                    };
                }
            }

            string yorumQuery = "SELECT * FROM Yorumlar WHERE BlogId = @bid ORDER BY Tarih DESC";
            SqlCommand cmdY = new SqlCommand(yorumQuery, conn);
            cmdY.Parameters.AddWithValue("@bid", id);
            using (SqlDataReader drY = cmdY.ExecuteReader())
            {
                while (drY.Read())
                {
                    yorumListesi.Add(new
                    {
                        Kullanici = drY["KullaniciAdi"].ToString(),
                        Metin = drY["YorumMetni"].ToString(),
                        Tarih = Convert.ToDateTime(drY["Tarih"])
                    });
                }
            }
        }

        if (blog == null) return RedirectToAction("Bloglar");
        ViewBag.Yorumlar = yorumListesi;
        return View(blog);
    }

    // --- YENİ YORUM EKLEME (SADECE ÜYELER) ---
    [HttpPost]
    public IActionResult YorumEkle(int BlogId, string YorumMetni)
    {
        // GİRİŞ YAPMAYAN YORUM ATAMAZ
        if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string gonderenIsmi = User.Identity.Name;

            // EĞER VETERİNERSE UNVAN EKLE
            if (User.IsInRole("Veteriner"))
            {
                gonderenIsmi += " (Veteriner Hekim) 🩺";
            }

            string query = "INSERT INTO Yorumlar (BlogId, KullaniciAdi, YorumMetni, Tarih) VALUES (@bid, @k, @m, @t)";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@bid", BlogId);
            cmd.Parameters.AddWithValue("@k", gonderenIsmi);
            cmd.Parameters.AddWithValue("@m", YorumMetni);
            cmd.Parameters.AddWithValue("@t", DateTime.Now);

            conn.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Bloglar_detay", new { id = BlogId });
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index");
    }
}