﻿using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using SecurityEssentials.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Text.RegularExpressions;
using System.Configuration;

namespace SecurityEssentials.Core.Identity
{

    public class MyUserManager : IUserManager, IDisposable
    {

        #region Declarations

        private readonly UserStore<User> UserStore;
        private readonly SEContext Context;
        private readonly string PasswordValidityRegex = @"^.*(?=.{8,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[a-zA-Z0-9]).*$";
        private readonly string PasswordValidityMessage = "Your password must consist of 8 characters, digits or special characters and must contain at least 1 uppercase, 1 lowercase and 1 numeric value";

        #endregion

        #region Properties

		private static IAuthenticationManager AuthenticationManager
		{
			get
			{
				return HttpContext.Current.GetOwinContext().Authentication;
			}
		}

        #endregion

        #region Constructor

        public MyUserManager()
        {
            Context = new SEContext();
            UserStore = new UserStore<User>(Context);
        }

        #endregion

        #region Create

        public async Task<SEIdentityResult> CreateAsync(string userName, string firstName, string lastName, string password, string passwordConfirmation, int securityQuestionLookupItemId, string securityAnswer)
        {
            var user = await UserStore.FindByNameAsync(userName);

			var result = ValidatePassword(password, new List<string>() {firstName, lastName, securityAnswer});
			if (result.Succeeded)
			{

				if (user == null)
				{
					user = new User();
					user.UserName = userName;
					var securedPassword = new SecuredPassword(password);
					try
					{
						user.Approved = Convert.ToBoolean(ConfigurationManager.AppSettings["AccountManagementRegisterAutoApprove"].ToString());
						user.EmailConfirmationToken = Guid.NewGuid().ToString().Replace("-", "");
						user.EmailVerified = false;
						user.Enabled = true;
						user.FirstName = firstName;
						user.LastName = lastName;
						user.PasswordHash = Convert.ToBase64String(securedPassword.Hash);
						user.Salt = Convert.ToBase64String(securedPassword.Salt);
						user.SecurityQuestionLookupItemId = securityQuestionLookupItemId;
                        var encryptor = new Encryption();
                        string encryptedSecurityAnswer = "";
                        encryptor.Encrypt(ConfigurationManager.AppSettings["encryptionPassword"], user.Salt,
                            Convert.ToInt32(ConfigurationManager.AppSettings["encryptionIterationCount"]), securityAnswer, out encryptedSecurityAnswer);

                        user.SecurityAnswer = encryptedSecurityAnswer;
						user.UserName = userName;
						user.UserLogs.Add(new UserLog() { Description = "Account Created" });
						await UserStore.CreateAsync(user);
					}
					catch
					{
						return new SEIdentityResult("An error occurred creating the user, please contact the system administrator");
					}

					return new SEIdentityResult();
				}
				
				// TODO: Log duplicate account creation
				return new SEIdentityResult("Username already registered");
			}
			else
			{
				return result;
			}
        }

        #endregion

        #region IDisposable Implemented Methods

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (this.Context != null)
                {
                    this.Context.Dispose();
                }
                if (this.UserStore != null)
                {
                    this.UserStore.Dispose();
                }
            }
        }

        #endregion

        #region Find

        public async Task<User> FindById(int userId)
        {
            return await UserStore.FindByIdAsync(userId).ConfigureAwait(false);
        }

		public async Task<LogonResult> FindAsync(string userName, string password)
        {
            return await UserStore.FindAndCheckLogonAsync(userName, password).ConfigureAwait(false);
        }

		#endregion

        #region SignInOut

        public async Task SignInAsync(string userName, bool isPersistent)
        {
            var user = await UserStore.FindByNameAsync(userName);
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
            var identity = await UserStore.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, identity);
			user.UserLogs.Add(new UserLog() { Description = "User Logged On" });
			await UserStore.UpdateAsync(user);
        }

		public void SignOut()
        {
			try
			{
				var userName = AuthenticationManager.User.Identity.Name;
				using (var context = new SEContext())
				{
					var user = context.User.Where(u => u.UserName == userName).FirstOrDefault();
					user.UserLogs.Add(new UserLog() { Description = "User Logged Off" });
					context.SaveChanges();
				}
			}
			catch {

				}
			finally
			{
				AuthenticationManager.SignOut();
			}
        }

        #endregion

        #region Change

		public async Task<SEIdentityResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
		{
			var user = await FindById(userId);
			var result = ValidatePassword(newPassword, new List<string>() { user.FirstName, user.LastName, user.SecurityAnswer });
			if (result.Succeeded)
			{
				await UserStore.ChangePasswordAsync(userId, oldPassword, newPassword);
				return new SEIdentityResult();
			}
			else
			{
				return result;
			}

		}

		public async Task<SEIdentityResult> ChangePasswordFromTokenAsync(int userId, string oldPassword, string newPassword)
		{
			var user = await FindById(userId);
			var result = ValidatePassword(newPassword, new List<string>() { user.FirstName, user.LastName, user.SecurityAnswer });
			if (result.Succeeded)
			{
				await UserStore.ChangePasswordFromTokenAsync(userId, oldPassword, newPassword);
				return new SEIdentityResult();
			}
			else
			{
				return result;
			}
		}

        #endregion

		public SEIdentityResult ValidatePassword(string password, List<string> bannedWords)
		{
			if (Regex.Matches(password, PasswordValidityRegex).Count == 0)
			{
				return new SEIdentityResult(PasswordValidityMessage);
			}

			var badPassword = Context.LookupItem.Where(l => l.LookupTypeId == CONSTS.LookupTypeId.BadPassword && l.Description == password.ToLower()).FirstOrDefault();
			if (badPassword != null)
			{
				return new SEIdentityResult("Your password is on a list of easy to guess passwords, please choose another");
			}

			foreach (string bannedWord in bannedWords)
			{
				if (password.IndexOf(bannedWord, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return new SEIdentityResult("Your password cannot contain any of your personal information");
				}
			}
			return new SEIdentityResult();
		}
    }
}