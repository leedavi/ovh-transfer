using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using net.openstack.Core.Domain;
using net.openstack.Core.Providers;
using net.openstack.Providers.Rackspace;

namespace ovh_transfer
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                CopyToOvh();
                Console.WriteLine("OK," + DateTime.UtcNow);
                var containerName = GetSetting("containername");
                SendEamil(true, "OK," + DateTime.UtcNow, containerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                var containerName = GetSetting("containername");
                SendEamil(false, ex.ToString(), containerName);
            }

        }

        static String GetSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[key] ?? "";
                return result;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
                return "";
            }
        }

        private static void CopyToOvh()
        {
            Console.WriteLine("Copy to OVH");

            var username = GetSetting("username");
            var password = GetSetting("password");
            var projectname = GetSetting("projectname");
            var containerName = GetSetting("containername");
            var uri = GetSetting("uri");
            var zipFolder = GetSetting("zipfolder");

            var zipFs = zipFolder.Split(';');

            foreach (var zipF in zipFs)
            {

                var identityEndpoint = new Uri(uri);
                var identity = new CloudIdentityWithProject
                {
                    Username = username,
                    Password = password,
                    ProjectName = projectname
                };

                var identityProvider = new OpenStackIdentityProvider(identityEndpoint, identity);
                // Verify that we can connect and our credentials are correct
                identityProvider.Authenticate();

                var provider = new CloudFilesProvider(identity, identityProvider);


                var filelist = Directory.GetFiles(zipF, "*.zip");
                if (filelist.Any())
                {
                    foreach (var filePath in filelist)
                    {
                        Console.WriteLine("Copy OpenStack: " + DateTime.UtcNow + " " + filePath);

                        var objectName = Path.GetFileName(filePath);
                        // Method 2: Set metadata in a separate call after the object is created
                        provider.CreateObjectFromFile(containerName, filePath, objectName, null, 4096, null, "GRA1");
                        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            {"Key", "Value"}
                        };
                        provider.UpdateObjectMetadata(containerName, objectName, metadata, "GRA1");
                    }
                }
            }
        }

        private static void SendEamil(bool isOK, string emailMsg,string containername)
        {
            var smtp = GetSetting("smtp");
            var smtpuser = GetSetting("smtpuser");
            var smtppassword = GetSetting("smtppassword");
            var email = GetSetting("email");

            Console.WriteLine(emailMsg);

            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(smtp);

                mail.From = new MailAddress(email);
                mail.To.Add(email);
                if (isOK)
                    mail.Subject = "OVH Transfer: " + containername + " OK";
                else
                    mail.Subject = "OVH Transfer: " + containername + " FAILED";
                mail.Body = emailMsg;

                SmtpServer.Port = 25;
                SmtpServer.Credentials = new System.Net.NetworkCredential(smtpuser, smtppassword);
                SmtpServer.EnableSsl = false;

                SmtpServer.Send(mail);
            }
            catch (Exception e)
            {
                Console.WriteLine("Email Error : " + e.ToString() + Environment.NewLine);
            }

        }

    }
}
