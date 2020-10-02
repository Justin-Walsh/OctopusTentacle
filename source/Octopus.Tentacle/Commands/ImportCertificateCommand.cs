﻿using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Security;
using Octopus.Shared.Security.Certificates;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class ImportCertificateCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly ILog log;
        bool fromRegistry;
        string importFile;
        string importPfxPassword;

        public ImportCertificateCommand(Lazy<ITentacleConfiguration> tentacleConfiguration, ILog log, IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("r|from-registry", "Import the Octopus Tentacle 1.x certificate from the Windows registry", v => fromRegistry = true);
            Options.Add("f|from-file=", "Import a certificate from the specified file generated by the new-certificate command or a Personal Information Exchange (PFX) file", v => importFile = v);
            Options.Add("pw|pfx-password=", "Personal Information Exchange (PFX) private key password", v => importPfxPassword = v, sensitive: true);
        }

        protected override void Start()
        {
            base.Start();
            if (!fromRegistry && string.IsNullOrWhiteSpace(importFile))
                throw new ControlledFailureException("Please specify the certificate to import.");

            if (fromRegistry && !string.IsNullOrWhiteSpace(importFile))
                throw new ControlledFailureException("Please specify only one of either from-registry or from-file.");

            X509Certificate2 x509Certificate = null;
            if (fromRegistry)
            {
                log.Info("Importing the Octopus 1.x certificate stored in the Windows registry...");

                string encoded = GetEncodedCertificate();
                if (string.IsNullOrWhiteSpace(encoded))
                {
                    throw new ControlledFailureException("No Octopus 1.x Tentacle certificate was found.");
                }
                x509Certificate = CertificateEncoder.FromBase64String(encoded);
            }
            else if (!string.IsNullOrWhiteSpace(importFile))
            {
                if (!File.Exists(importFile))
                    throw new ControlledFailureException($"Certificate '{importFile}' was not found.");
                
                var fileExtension = Path.GetExtension(importFile);

                //We assume if the file does not end in .pfx that it is the legacy base64 encoded certificate, however if this fails we should still attempt to read as the PFX format.
                if (fileExtension.ToLower() != ".pfx")
                {
                    try
                    {
                        log.Info($"Importing the certificate stored in {importFile}...");
                        var encoded = File.ReadAllText(importFile, Encoding.UTF8);
                        x509Certificate = CertificateEncoder.FromBase64String(encoded);
                    }
                    catch (FormatException)
                    {
                        x509Certificate = CertificateEncoder.FromPfxFile(importFile, importPfxPassword);
                    }
                }
                else
                {
                    x509Certificate = CertificateEncoder.FromPfxFile(importFile, importPfxPassword);
                }
            }

            if (x509Certificate == null)
                throw new Exception("Failed to retrieve certificate with the parameters specified.");

            tentacleConfiguration.Value.ImportCertificate(x509Certificate);
            VoteForRestart();

            if (x509Certificate.PrivateKey.KeySize < CertificateGenerator.RecommendedKeyBitLength)
                log.Warn("The imported certificate's private key is smaller than the currently-recommended bit length; generating a new key for the tentacle is advised.");

            log.Info($"Certificate with thumbprint {x509Certificate.Thumbprint} imported successfully.");
        }

        string GetEncodedCertificate()
        {
            const RegistryHive Hive = RegistryHive.LocalMachine;
            const RegistryView View = RegistryView.Registry64;
            const string KeyName = "Software\\Octopus";

#pragma warning disable PC001 //API not supported on all platforms, this code should only be run on Windows
            using (var key = RegistryKey.OpenBaseKey(Hive, View))
            using (var subkey = key.OpenSubKey(KeyName, false))
            {
                if (subkey != null)
                {
                    return (string)subkey.GetValue("Cert-cn=Octopus Tentacle", null);
                }

                return null;
            }
#pragma warning restore PC001
        }
    }
}