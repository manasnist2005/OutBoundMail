using OutBoundMail.SmartTargetService;
using System;
using System.Collections.Generic;
using System.Data.Services.Client;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Tridion.AudienceManagement.API;


namespace OutBoundMail
{
    public static class Subscription
    {

        /// <summary>
        /// Returns the list of Email addresses and the Html content to be sent to the user
        /// Creates a client to the Odata service and connects to the Audience Manager database
        /// </summary>
        /// <returns>List<EmailWithHtmlContent></returns>
        public static List<EmailWithHtmlContent> GetEmailContentList()
        {

            List<EmailWithHtmlContent> emailWithHtmlContents = new List<EmailWithHtmlContent>();
            List<CompPresentationId> componentPresentationIdList = new List<CompPresentationId>();
            ContentDeliveryService odataclient = new ContentDeliveryService(new Uri("http://localhost:84/odata.svc/", UriKind.Absolute));         
            List<Promotion> promotionList = odataclient.Promotions.ToList();
            List<Keyword> keywordList = odataclient.Keywords.ToList();
            System.Data.Services.Client.DataServiceQuery<ComponentPresentation> componentPresentations = odataclient.ComponentPresentations;
            try
            {
               
                var addressBookUri = new TcmUri(2033, 3, TcmItemTypes.StaticAddressBook);
                var addressBook = new StaticAddressBook(addressBookUri);
                var contacts = addressBook.GetContacts();

                foreach (Contact contact in contacts)
                {
                    componentPresentationIdList = GetPromotionCPs(contact, promotionList, GetSubscriptionsforUser(contact, keywordList));
                    if(componentPresentationIdList.Count > 0)
                    { 
                        List<EmailContents> eMailContents = GetContentForEmail(componentPresentationIdList,componentPresentations);                    
                        EmailWithHtmlContent emailWithHtmlContent = new EmailWithHtmlContent();
                        emailWithHtmlContent.eMail = contact.EmailAddress.ToString();
                        emailWithHtmlContent.htmlView = getHtmlBodyforEmail(eMailContents, contact.ExtendedDetails["NAME"].StringValue);
                        emailWithHtmlContents.Add(emailWithHtmlContent);
                    }
                    
                }
                return emailWithHtmlContents;
            }           
            catch (Exception ex)
            {
                return emailWithHtmlContents;               
            }


        }

        /// <summary>
        /// Get the list of ComponentPresentation list from the promotions belonged to a particular contact
        /// </summary>
        /// <param name="contact"></param>
        /// <param name="promotionList"></param>
        /// <param name="lstSubscription"></param>
        /// <returns></returns>
        public static List<CompPresentationId> GetPromotionCPs(Contact contact, List<Promotion> promotionList, List<string> lstSubscription)
        {            
            List<CompPresentationId> componentPresentationIdList = new List<CompPresentationId>();
            if (lstSubscription.Count > 0 && promotionList.Count > 0)
            { 
                foreach (Promotion pr in promotionList)
                {

                    if (lstSubscription.Contains(pr.Name.ToUpper()))                
                    {
                        var strCPList = XElement.Parse(pr.Action).Nodes().ToList()[0].ToString()
                            .Replace("xmlns=\"http://schemas.datacontract.org/2004/07/Tridion.SmartTarget.OData\"", "");
                        var CPList = from component in XElement.Parse(strCPList).Elements("Item")
                                     select new CompPresentationId
                                        {
                                            componentId = component.Element("ComponentId").Value,
                                            componentTemplateId = component.Element("ComponentTemplateId").Value
                                        };

                        foreach (var cpId in CPList.ToList())
                        {
                            componentPresentationIdList.Add(cpId);
                        }
                    
                    }
                }
            }            
            return componentPresentationIdList;
        }

        /// <summary>
        /// Get all the subscribed keywords for a contact
        /// Keyword list is fetched from the odata service . Contact class is fetched from the Audience Manager API 
        /// </summary> 
        /// <param name="contact"></param>
        /// <param name="Keywords"></param>
        /// <returns>List<string></returns>
        public static List<string> GetSubscriptionsforUser(Contact contact,List<Keyword> Keywords)
        {
           List<string> lstSubscription = new List<string>();
           foreach(var kys in contact.Keywords)
           {
               if (Keywords.Count > 0)
               {
                   var titles = from keyword in Keywords
                                where keyword.Id == kys.ItemId
                                select keyword.Title;
                   if (titles.ToList().Count > 0)
                       lstSubscription.Add(titles.ToList()[0].ToString().ToUpper());
               }
           }            
           return lstSubscription;
        }

        /// <summary>
        /// Get the field values from the components present in the ComponentPresentation list 
        /// </summary>
        /// <param name="componentPresentationIdList"></param>
        /// <param name="componentPresentationslist"></param>
        /// <returns>List<EmailContents></returns>
        public static List<EmailContents> GetContentForEmail(List<CompPresentationId> componentPresentationIdList, DataServiceQuery<ComponentPresentation> componentPresentationslist)
        {
            Dictionary<string, string> emailContent = new Dictionary<string, string>();
            List<EmailContents> eMailContents = new List<EmailContents>();
            int counter = 1;

            try
            {                
                List<ComponentPresentation> componentPresentations = new List<ComponentPresentation>();
               

                foreach (var cp in componentPresentationIdList)
                {
                    EmailContents eMailContent = new EmailContents();                    
                    eMailContent.componentnumber = counter;

                    componentPresentations = componentPresentationslist
                        .Where(cpid => cpid.ComponentId == Convert.ToInt32(cp.componentId.Split('-')[1].ToString()) &&
                            cpid.TemplateId == Convert.ToInt32((cp.componentTemplateId.Split('-'))[1].ToString()))
                            .Take(10).ToList();

                    XElement xElement = XElement.Parse(componentPresentations[0].PresentationContent.ToString());
                    var ComponentFields = xElement.Element("Fields").Elements("item").ToList();
                    foreach (var field in ComponentFields)
                    {
                        FieldDetails fieldDetail = new FieldDetails();                       
                        if (field.Element("value").Element("Field").Attribute("FieldType").Value == "Text" ||
                           field.Element("value").Element("Field").Attribute("FieldType").Value == "MultiLineText")
                        {
                            fieldDetail.fieldType = field.Element("value").Element("Field").Attribute("FieldType").Value;
                            fieldDetail.fieldTitle = field.Element("key").Value;
                            fieldDetail.fieldValue = field.Element("value").Element("Field").Element("Values").Value;
                        }
                        if (field.Element("value").Element("Field").Attribute("FieldType").Value == "MultiMediaLink")
                        {
                            fieldDetail.fieldType = field.Element("value").Element("Field").Attribute("FieldType").Value;
                            fieldDetail.fieldTitle = field.Element("key").Value;
                            fieldDetail.fieldValue = field.Element("value").Element("Field").Element("LinkedComponentValues")
                                                        .Element("Component").Element("Multimedia").Element("Url").Value;
                        }
                        eMailContent.fieldDetails.Add(fieldDetail);
                    }
                    counter++;
                    eMailContents.Add(eMailContent);
                }
                return eMailContents;
            }
            catch (Exception ex)
            {
                string ss = ex.Message;
                return eMailContents;
            }
        }

        /// <summary>
        /// Creates an html content which will be attached to a mail body.
        /// </summary>
        /// <param name="eMailContents"></param>
        /// <returns>AlternateView</returns>
        public static AlternateView getHtmlBodyforEmail(List<EmailContents> eMailContents, string name)
        {
            List<LinkedResource> lstLinkedResources = new List<LinkedResource>();
            string currentDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            string html = File.ReadAllText(currentDirectory+"\\EmailTemplate.html");
           // string content1 = Template.GetEmailComponent1();
            //string componentTemplate1=string.Concat(content1, ".html");
            //string compohtmlTemplate1 = File.ReadAllText(currentDirectory + "\\"+ componentTemplate1);
            string content = GetEmailComponent();
            string compohtmlTemplate = content;
            //AlternateView alternateView;
            string componentBody = string.Empty;
            foreach (var emailcontent in eMailContents)
            {
                string header = string.Empty; string description = string.Empty; string linkUrl = string.Empty; string imagesrc = string.Empty;
                foreach (var field in emailcontent.fieldDetails)
                {                   
                    if (field.fieldTitle == "header")
                        header = field.fieldValue.ToString();
                    if (field.fieldTitle == "description")
                        description = field.fieldValue.ToString();
                    if (field.fieldTitle == "linkUrl")
                        linkUrl = field.fieldValue.ToString();
                    if (field.fieldTitle == "image")
                    {
                        try
                        {
                            string imagePath = "C:\\Tridion_File_Deployer\\DD4T_Staging\\data\\pub2033" + field.fieldValue.ToString().Replace("%20", " ").Replace("/", "\\");
                            LinkedResource inline = new LinkedResource(imagePath);
                            inline.ContentId = Guid.NewGuid().ToString();
                            imagesrc = @"<img src='cid:" + inline.ContentId + @"' width='50%' height='30%' alt='" + header + "'/>";
                            lstLinkedResources.Add(inline);
                        }
                        catch(Exception ex)
                        {
                            imagesrc = header;
                        }
                    }                     
                }
                //componentBody += string.Format(compohtmlTemplate1, header, description, imagesrc, linkUrl);
                componentBody += string.Format(compohtmlTemplate, header, description, imagesrc, linkUrl); 
            }
            string htmlBody = string.Format(html, componentBody, name);
            AlternateView alternateView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);
            foreach (var lnkdResource in lstLinkedResources)
            {
                alternateView.LinkedResources.Add(lnkdResource);
            }
            return alternateView;
        }
        //public static string GetEmailComponent1()
        //{
        //    string CDWS_URL = "http://localhost:84/odata.svc";
        //    var service = new ContentDeliveryService(new Uri(CDWS_URL));
        //    string content1 = GetCPContentByUri1(service, "tcm:2033-6060");
        //    return content1;
        //}
        public static string GetEmailComponent()
        {
            string CDWS_URL = "http://localhost:84/odata.svc";
            var service = new ContentDeliveryService(new Uri(CDWS_URL));
            string content = GetCPContentByUri(service, "tcm:2033-6060");
            return content;
        }
        //private static string GetCPContentByUri1(ContentDeliveryService service, string uri)
        //{

        //    string result = string.Empty;
        //    TcmUri tcmUri = new TcmUri(uri);

        //    var cp = (from x in service.ComponentPresentations
        //              where x.PublicationId == tcmUri.PublicationId &&
        //                    x.ComponentId == tcmUri.ItemId
        //              select x).FirstOrDefault();

        //    if (cp != null)
        //    {
        //        XElement xElement1 = XElement.Parse(cp.PresentationContent.ToString());
        //        var ComponentFields = xElement1.Element("Fields").Elements("item").ToList();
        //        var ct = ComponentFields[0].Element("value").Element("Field").Element("Values").Value;
        //        return ct.ToString();
        //    }
        //    else
        //    {
        //        return "Template not exist";
        //    }
        //}
        private static string GetCPContentByUri(ContentDeliveryService service, string uri)
        {

            string result = string.Empty;
            TcmUri tcmUri = new TcmUri(uri);

            var cp = (from x in service.ComponentPresentations
                      where x.PublicationId == tcmUri.PublicationId &&
                            x.ComponentId == tcmUri.ItemId
                      select x).FirstOrDefault();

            if (cp != null)
            {
                XElement xElement = XElement.Parse(cp.PresentationContent.ToString());
                var ComponentFields = xElement.Element("Fields").Elements("item").ToList();
                var ct = ComponentFields[0].Element("value").Element("Field").Element("Keywords").Element("Keyword").Attribute("Key").Value;
                var cpr = (from x in service.ComponentPresentations
                           where x.PublicationId == tcmUri.PublicationId &&
                                 x.TemplateId == Convert.ToInt32(ct.ToString())
                           select x).FirstOrDefault();
                if (cpr != null)
                {
                    XElement xElement2 = XElement.Parse(cpr.PresentationContent.ToString());
                    return xElement2.ToString();
                }
                else
                    return "No component Presentation";


            }
            else
            {
                return "Template not exist";
            }
        }
    }
   
    public class CompPresentationId
    {
        public string componentId { get; set; }
        public string componentTemplateId { get; set; }
    }

    public class EmailContents
    {
        public int componentnumber { get; set; }        
        private List<FieldDetails> _fielddetails;
        public List<FieldDetails> fieldDetails
        {
            get { return _fielddetails ?? (_fielddetails = new List<FieldDetails>()); }
            set { _fielddetails = value; }
        }
    }
    public class FieldDetails
    {
        public string fieldType { get; set; }
        public string fieldTitle { get; set; }
        public string fieldValue { get; set; }
    }

    public class EmailWithHtmlContent
    {
        public string eMail { get; set; }
        public AlternateView htmlView { get; set; }
    }
}
