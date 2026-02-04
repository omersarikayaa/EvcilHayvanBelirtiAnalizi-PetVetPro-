using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System;

namespace EvcilHayvanBelirtiAnalizi.Controllers
{
    // Sadece Admin girişi yapınca çalışması için [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")] mühürlenebilir
    public class AdminController : Controller
    {
        string connStr = "Server=DESKTOP-QI04ERP\\SQLEXPRESS;Database=EvcilHayvanBelirtiAnalizDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // --- 1. VETERİNER ONAY İŞLEMLERİ ---
        public IActionResult OnayBekleyenler()
        {
            var liste = new List<dynamic>();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = "SELECT KullaniciId, KullaniciAdi, Email, BelgeYolu FROM Kullanicilar WHERE Rol='Veteriner' AND Onayli=0";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    liste.Add(new
                    {
                        Id = dr["KullaniciId"],
                        Ad = dr["KullaniciAdi"].ToString(),
                        Email = dr["Email"].ToString(),
                        Belge = dr["BelgeYolu"].ToString()
                    });
                }
            }
            return View(liste);
        }

        [HttpPost]
        public IActionResult VeterinerOnayla(int id)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = "UPDATE Kullanicilar SET Onayli=1 WHERE KullaniciId=@id";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("OnayBekleyenler");
        }

        // --- 2. BLOG YÖNETİMİ (YENİ EKLENDİ) ---
        // Tüm blogları listele ki admin silebilsin
        public IActionResult BlogYonetimi()
        {
            var liste = new List<dynamic>();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = "SELECT B.Id, B.Baslik, B.YayinTarihi, K.KullaniciAdi FROM Bloglar B INNER JOIN Kullanicilar K ON B.VeterinerId = K.KullaniciId ORDER BY B.YayinTarihi DESC";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    liste.Add(new
                    {
                        Id = dr["Id"],
                        Baslik = dr["Baslik"].ToString(),
                        Tarih = Convert.ToDateTime(dr["YayinTarihi"]),
                        Yazar = dr["KullaniciAdi"].ToString()
                    });
                }
            }
            return View(liste);
        }

        // Blog Silme İşlemi
        [HttpPost]
        public IActionResult BlogSil(int id)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = "DELETE FROM Bloglar WHERE Id=@id";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("BlogYonetimi");
        }
    }
}