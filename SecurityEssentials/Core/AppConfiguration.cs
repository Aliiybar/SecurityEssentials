﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace SecurityEssentials.Core
{

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>TODO: This should be created as a singleton using your DI framework</remarks>
    public class AppConfiguration : IAppConfiguration
    {

        public bool AccountManagementRegisterAutoApprove { get; private set; }
        public string ApplicationName { get; private set; }
        public string DefaultFromEmailAddress { get; private set; }
        public string EncryptionPassword { get; private set; }
        public int EncryptionIterationCount { get; private set; }
        public bool HasRecaptcha { get; private set; }
        public string WebsiteBaseUrl { get; private set; }

        public AppConfiguration()
        {
            AccountManagementRegisterAutoApprove = Convert.ToBoolean(ConfigurationManager.AppSettings["AccountManagementRegisterAutoApprove"]);
            ApplicationName = ConfigurationManager.AppSettings["ApplicationName"];
            DefaultFromEmailAddress = ConfigurationManager.AppSettings["DefaultFromEmailAddress"];
            EncryptionPassword = ConfigurationManager.AppSettings["EncryptionPassword"];
            EncryptionIterationCount = Convert.ToInt32(ConfigurationManager.AppSettings["EncryptionIterationCount"]);
            HasRecaptcha = Convert.ToBoolean(ConfigurationManager.AppSettings["HasRecaptcha"]);
            WebsiteBaseUrl = ConfigurationManager.AppSettings["WebsiteBaseUrl"];
        }
    }
}