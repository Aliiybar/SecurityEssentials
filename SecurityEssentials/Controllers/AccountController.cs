﻿using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SecurityEssentials.Model;
using SecurityEssentials.Core.Identity;
using SecurityEssentials.Core;

namespace SecurityEssentials.Controllers
{
    public class AccountController : AntiForgeryControllerBase
    {

		private IUserManager UserManager;

        #region Constructor

        public AccountController()            
        {
			UserManager = new MyUserManager();
        }

        #endregion

        #region LogOff

        [HttpPost]
        [Authorize]
        public ActionResult LogOff()
        {
            UserManager.SignOut();
            Session.Abandon();
			return RedirectToAction("LogOn");
        }

        #endregion

        #region LogOn

        [AllowAnonymous]
        public ActionResult LogOn(string returnUrl)
        {
            if (Request.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View("LogOn");
        }

        [HttpPost]
        [AllowAnonymous]
        [AllowXRequestsEveryXSecondsAttribute(Name = "LogOn", Message = "You have performed this action more than {x} times in the last {n} seconds.", Requests = 3, Seconds = 60)]
        public async Task<ActionResult> LogOn(LogOn model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindAsync(model.UserName, model.Password);
                if (user != null)
                {
                    await UserManager.SignInAsync(user.UserName, model.RememberMe);
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("", "Invalid username/password or account is locked");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        #endregion

        #region Manage

        public ActionResult Manage(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            ViewBag.ReturnUrl = Url.Action("Manage");
            return View();
        }

        [HttpPost]
        [Authorize]
        [AllowXRequestsEveryXSecondsAttribute(Name = "ManageUser", Message = "You have performed this action more than {x} times in the last {n} seconds.", Requests = 2, Seconds = 60)]
        public async Task<ActionResult> Manage(ManageUser model)
        {
            ViewBag.ReturnUrl = Url.Action("Manage");
            var result = await UserManager.ChangePasswordAsync(Convert.ToInt32(User.Identity.GetUserId()), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
				// TODO: Email recipient with password change acknowledgement
                return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            else
            {
                AddErrors(result);
            }
            return View(model);
        }

        #endregion

        #region Recover

        [AllowAnonymous]
        public ActionResult Recover()
        {
            return View("Recover");
        }

        [HttpPost]
        [AllowAnonymous]
        [AllowXRequestsEveryXSecondsAttribute(Name = "Recover", ContentName = "TooManyRequests", Requests = 2, Seconds = 60)]
        public async Task<ActionResult> Recover(Recover model)
        {
            if (ModelState.IsValid)
            {
                ViewBag.StatusMessage = "Your request has been processed. If the email you entered was valid and your account active then a reset email will be sent to your address";
                var user = await UserManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    if (user.Enabled == true)
                    {
                        await UserManager.GeneratePasswordResetTokenAsync(user.Id);
						// TODO: Send recovery email
                    }
                }
            }

            return View(model);
        }

        [AllowAnonymous]
        public ActionResult RecoverPassword()
        {
            var passwordResetToken = Request["PasswordResetToken"] ?? "";
			using (var context = new SEContext())
			{
				var user = context.User.Where(u => u.PasswordResetToken == passwordResetToken).FirstOrDefault();
				if (user == null)
				{
					HandleErrorInfo error = new HandleErrorInfo(new ArgumentException("INFO: The password recovery token is not valid or has expired"), "Account", "RecoverPassword");
					return View("Error", error);
				}
				if (user.Enabled == false)
				{
					HandleErrorInfo error = new HandleErrorInfo(new InvalidOperationException("INFO: Your account is not currently approved or active"), "Account", "Recover");
					return View("Error", error);
				}
				RecoverPassword recoverPasswordModel = new RecoverPassword()
				{
					Id = user.Id,
					SecurityAnswer = "",
					SecurityQuestion = user.SecurityQuestion,
					PasswordResetToken = passwordResetToken,
					UserName = user.UserName
				};
				return View("RecoverPassword", recoverPasswordModel);
			}
        }

        [HttpPost]
        [AllowAnonymous]
        [AllowXRequestsEveryXSecondsAttribute(Name = "RecoverPassword", ContentName = "TooManyRequests", Requests = 2, Seconds = 60)]
        public async Task<ActionResult> RecoverPassword(RecoverPassword recoverPasswordModel)
        {
			using (var context = new SEContext())
			{
				var user = context.User.Where(u => u.Id == recoverPasswordModel.Id).FirstOrDefault();
				if (user == null)
				{
					HandleErrorInfo error = new HandleErrorInfo(new Exception("INFO: The user is not valid"), "Account", "RecoverPassword");
					return View("Error", error);
				}
				if (!(user.Enabled))
				{
					HandleErrorInfo error = new HandleErrorInfo(new Exception("INFO: Your account is not currently approved or active"), "Account", "Recover");
					return View("Error", error);
				}
				if (user.SecurityAnswer != recoverPasswordModel.SecurityAnswer)
				{
					ModelState.AddModelError("SecurityAnswer", "The security answer is incorrect");
					return View("RecoverPassword", recoverPasswordModel);
				}
				if (recoverPasswordModel.Password != recoverPasswordModel.ConfirmPassword)
				{
					ModelState.AddModelError("ConfirmPassword", "The passwords do not match");
					return View("RecoverPassword", recoverPasswordModel);
				}

				if (ModelState.IsValid)
				{
					var result = await UserManager.ChangePasswordFromTokenAsync(user.Id, recoverPasswordModel.PasswordResetToken, recoverPasswordModel.Password);
					if (result.Succeeded)
					{
						context.SaveChanges();
						await UserManager.SignInAsync(user.UserName, false);
						return View("RecoverPasswordSuccess");
					}
					else
					{
						AddErrors(result);
						return View("RecoverPassword", recoverPasswordModel);
					}
				}
				else
				{
					ModelState.AddModelError("", "Password change was not successful");
					return View("RecoverPassword", recoverPasswordModel);
				}
			}
        }

        #endregion

        #region Register

        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> Register(Register model)
        {
            if (ModelState.IsValid)
            {
				var result = await UserManager.CreateAsync(model.UserName, model.FirstName, model.LastName, model.Password, model.ConfirmPassword, model.Email);
                if (result.Succeeded)
                {
                    await UserManager.SignInAsync(model.UserName, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        #endregion

        #region Helper Functions

        private void AddErrors(SEIdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
            return RedirectToAction("Index", "Home");
            }
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            Error
        }

        #endregion

    }
}