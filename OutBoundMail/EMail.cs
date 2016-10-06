using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using OutBoundMail.SmartTargetService;
using System.Xml.Linq;
using System.Net.Mime;

namespace OutBoundMail
{
    class EMail
    {
        static void Main(string[] args)
        {
            List<EmailWithHtmlContent> emails = Subscription.GetEmailContentList();
            foreach (EmailWithHtmlContent email in emails)
            {
                if (email.eMail.Contains("@tieto.com"))
                    SendMail("Tieto.Gadgets@tieto.com", email.eMail, email.htmlView);
            }
            //SendMail("Tieto.Gadgets@tieto.com", "manas.panda@tieto.com", emails[0].htmlView);
        }
        
        public static void SendMail(string fromemail,string tomail,AlternateView alternteview)
        {
            try
            {
                MailMessage mail = new MailMessage(fromemail, tomail);
                mail.IsBodyHtml = true;                 
                mail.AlternateViews.Add(alternteview);                
                mail.Subject = "Promotion";              
                SmtpClient client = new SmtpClient();          
                client.Port = 25;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = true;
                client.Host = "mailrelay.tieto.com";                
                client.Send(mail);
            }
            catch (Exception Ex)
            {
                string ss = Ex.InnerException.Message.ToString();
            }
        }
        
    }
}
