using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace EvcilHayvanBelirtiAnalizi.Services
{
    public class EmailService
    {
        public async Task SendEmailAsync(string email, string konu, string mesaj)
        {
            // BURAYA: Yeni açtığın Gmail adresini tam olarak yaz
            var senderEmail = "petvetprodestek@gmail.com";
            // BURAYA: Aldığın o efsane 16 haneli kodu yapıştırıyoruz
            var senderPass = "kzyujhhdjyhlbsyr";

            using (var client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(senderEmail, senderPass);
                client.Timeout = 20000;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "PetVetPro Destek"),
                    Subject = konu,
                    Body = mesaj,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                try
                {
                    await client.SendMailAsync(mailMessage);
                }
                catch (Exception ex)
                {
                    throw new Exception("Gmail Gönderim Hatası: " + ex.Message);
                }
            }
        }
    }
}