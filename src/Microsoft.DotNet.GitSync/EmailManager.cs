// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CredentialManagement;
using System;
using System.Net;
using System.Net.Mail;

namespace gitsync
{
    internal class EmailManager
    {
        private string _sender;
        private string _destinations;
        private SmtpClient _client { get; set; }

        public EmailManager(string server, string destinations)
        {
            using (var cred = new Credential())
            {
                cred.Target = "email";
                if (cred.Exists() && !string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(destinations))
                {
                    cred.Load();
                    _client = new SmtpClient(server, 587);
                    _sender = cred.Username;
                    _client.Credentials = new NetworkCredential(cred.Username, cred.Password);
                    _client.EnableSsl = true;
                    _destinations = destinations;
                    Program.logger.Info("Email Manager is configured correctly");
                }
                else
                {
                    Program.logger.Info("Email Manager is not able to find the correct configuration to use");
                }
            }
        }
       
        public void Email(string subject, string body)
        {
            try
            {
                if (_client != null)
                {
                    _client.Send(_sender, _destinations, subject, body);
                    Program.logger.Info("email send");
                }
                else
                {
                    Program.logger.Info("Email Manager is not able to find the correct configuration to use");
                }
            }
            catch (Exception ex)
            {
                Program.logger.Info("Sending email failed due to " + ex.Message);
            }
        }
    }
}
