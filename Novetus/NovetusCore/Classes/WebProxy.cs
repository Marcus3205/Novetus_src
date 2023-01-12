﻿#if LAUNCHER || URI
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace Novetus.Core
{
    public class IWebProxyExtension : IExtension
    {
        public virtual void OnProxyStart() { }
        public virtual void OnProxyStopped() { }

        public virtual bool IsValidURL(string absolutePath, string host) { return false; }

        public virtual Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e) { return Task.CompletedTask; }
        public virtual async Task OnRequest(object sender, SessionEventArgs e) 
        {
            e.Ok("Test successful. \nRunning Novetus " + GlobalVars.ProgramInformation.Version + " on " + GlobalVars.ProgramInformation.NetVersion);
        }
    }

    public class WebProxy
    {
        private ProxyServer Server = null;
        private ExplicitProxyEndPoint end;
        public ExtensionManager Manager = new ExtensionManager();

        public bool HasStarted()
        {
            return Server.ProxyRunning;
        }

        public void DoSetup()
        {
            if (GlobalVars.UserConfiguration.WebProxyInitialSetupRequired)
            {
                string text = "Would you like to enable the Novetus web proxy?\n\n" +
                    "A web proxy redirects web traffic to a different location and in some cases can act as a gateway to different sites. Novetus uses the web proxy for additional client features and asset redirection.\n\n" +
                    "When enabling the web proxy, Novetus will locally create a certificate upon startup that ensures the proxy's functionality. Novetus will not send any user data to anyone, as everything involving the web proxy is entirely local to this computer.\n" +
                    "If you have any issue connecting to other web sites, including Roblox, closing Novetus or typing 'proxy off' into Novetus' console will fix it in most instances.\n\n" +
                    "Upon pressing 'Yes', Windows will ask you for permission to install the certificate.\n\n" +
                    "You can change this option at any time by typing 'proxy disable' or 'proxy on' in the Novetus console. This message will appear only once.\n";

                DialogResult result = MessageBox.Show(text, "Novetus - Web Proxy Opt-In", MessageBoxButtons.YesNo);

                switch (result)
                {
                    case DialogResult.Yes:
                        GlobalVars.UserConfiguration.WebProxyEnabled = true;
                        Start();
                        break;
                    case DialogResult.No:
                    default:
                        break;
                }

                GlobalVars.UserConfiguration.WebProxyInitialSetupRequired = false;
                FileManagement.Config(GlobalPaths.ConfigDir + "\\" + GlobalPaths.ConfigName, true);
            }
            else
            {
                if (GlobalVars.UserConfiguration.WebProxyEnabled)
                {
                    Start();
                }
            }
        }

        public void Start()
        {
            Server = new ProxyServer();

            if (Server.ProxyRunning)
            {
                Util.ConsolePrint("The web proxy is already on and running.", 2);
                return;
            }

            try
            {
                Manager.LoadExtensions(GlobalPaths.NovetusExtsWebProxy);
                Util.ConsolePrint("Booting up Web Proxy...", 3);
                Server.CertificateManager.RootCertificateIssuerName = "Novetus";
                Server.CertificateManager.RootCertificateName = "Novetus Web Proxy";
                Server.BeforeRequest += new AsyncEventHandler<SessionEventArgs>(OnRequest);
                UpdateEndPoint(true);
                Util.ConsolePrint("Web Proxy started on port " + GlobalVars.WebProxyPort, 3);
                try
                {
                    foreach (IExtension extension in Manager.GetExtensionList().ToArray())
                    {
                        IWebProxyExtension webProxyExtension = extension as IWebProxyExtension;
                        if (webProxyExtension != null)
                        {
                            webProxyExtension.OnProxyStart();
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            catch (Exception e)
            {
                Util.LogExceptions(e);
            }
        }

        public void UpdateEndPoint(bool shouldRunServer = false, bool decrypt = true)
        {
            if (Server.ProxyEndPoints.Count > 0)
            {
                Server.RemoveEndPoint(end);
            }

            GlobalVars.WebProxyPort = GlobalVars.UserConfiguration.RobloxPort + 1;
            end = new ExplicitProxyEndPoint(IPAddress.Any, GlobalVars.WebProxyPort, decrypt);
            end.BeforeTunnelConnectRequest += new AsyncEventHandler<TunnelConnectSessionEventArgs>(OnBeforeTunnelConnectRequest);
            Server.AddEndPoint(end);

            if (!Server.ProxyRunning && shouldRunServer)
            {
                Server.Start();
            }

            if (Server.ProxyRunning)
            {
                foreach (ProxyEndPoint endPoint in Server.ProxyEndPoints)
                {
                    Server.SetAsSystemHttpProxy(end);
                    Server.SetAsSystemHttpsProxy(end);
                }
            }

            Util.ConsolePrint("Web Proxy Endpoint updated with port " + GlobalVars.WebProxyPort, 3);
        }

        private bool IsValidURL(HttpWebClient client)
        {
            string uri = client.Request.RequestUri.Host;

            if ((!uri.StartsWith("www.") &&
                !uri.StartsWith("web.") &&
                !uri.StartsWith("assetgame.") &&
                !uri.StartsWith("wiki.") &&
                !uri.EndsWith("api.roblox.com") &&
                !uri.StartsWith("roblox.com") || !uri.EndsWith("roblox.com")) &&
                !uri.EndsWith("robloxlabs.com"))
            {
                return false;
            }

            //we check the header
            HeaderCollection headers = client.Request.Headers;
            List<HttpHeader> userAgents = headers.GetHeaders("User-Agent");

            if (userAgents == null)
                return false;

            if (string.IsNullOrWhiteSpace(userAgents.FirstOrDefault().Value))
                return false;

            string ua = userAgents.FirstOrDefault().Value.ToLowerInvariant();

            //for some reason, this doesn't go through for the browser unless we look for mozilla/4.0.
            //this shouldn't break modern mozilla browsers though.
            return (ua.Contains("mozilla/4.0") || ua.Contains("roblox"));
        }

        private async Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            if (!IsValidURL(e.HttpClient))
            {
                e.DecryptSsl = false;
            }

            Uri uri = e.HttpClient.Request.RequestUri;

            foreach (IExtension extension in Manager.GetExtensionList().ToArray())
            {
                IWebProxyExtension webProxyExtension = extension as IWebProxyExtension;
                if (webProxyExtension != null)
                {
                    if (webProxyExtension.IsValidURL(uri.AbsolutePath.ToLowerInvariant(), uri.Host))
                    {
                        try
                        {
                            await webProxyExtension.OnBeforeTunnelConnectRequest(sender, e);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task OnRequest(object sender, SessionEventArgs e)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (!IsValidURL(e.HttpClient))
            {
                return;
            }

            Uri uri = e.HttpClient.Request.RequestUri;

            foreach (IExtension extension in Manager.GetExtensionList().ToArray())
            {
                IWebProxyExtension webProxyExtension = extension as IWebProxyExtension;
                if (webProxyExtension != null)
                {
                    if (webProxyExtension.IsValidURL(uri.AbsolutePath.ToLowerInvariant(), uri.Host))
                    {
                        try
                        {
                            await webProxyExtension.OnRequest(sender, e);
                            return;
                        }
                        catch (Exception)
                        {
                            e.GenericResponse("", HttpStatusCode.InternalServerError);
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            e.GenericResponse("", HttpStatusCode.NotFound);
        }

        public void Stop()
        {
            if (!Server.ProxyRunning)
            {
                Util.ConsolePrint("The web proxy is already turned off.", 2);
                return;
            }

            Util.ConsolePrint("Web Proxy stopping on port " + GlobalVars.WebProxyPort, 3);
            Server.BeforeRequest -= new AsyncEventHandler<SessionEventArgs>(OnRequest);
            Server.Stop();
            Server.Dispose();
            Server = null;

            foreach (IExtension extension in Manager.GetExtensionList().ToArray())
            {
                try
                {
                    IWebProxyExtension webProxyExtension = extension as IWebProxyExtension;
                    if (webProxyExtension != null)
                    {
                        webProxyExtension.OnProxyStopped();
                    }
                }
                catch (Exception)
                {
                }
            }

            Manager.UnloadExtensions();
            Manager.GetExtensionList().Clear();
        }
    }
}
#endif
