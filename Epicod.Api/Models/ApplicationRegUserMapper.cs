﻿using Fusi.Api.Auth.Controllers;
using Fusi.Api.Auth.Models;
using Fusi.Api.Auth.Services;
using System;
using System.Collections.Generic;

namespace Epicod.Api.Models
{
    public class ApplicationRegUserMapper : IUserMapper<ApplicationUser,
        NamedRegisterBindingModel, NamedUserModel>
    {
        public ApplicationUser GetModel(NamedRegisterBindingModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            return new ApplicationUser
            {
                UserName = model.Name,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };
        }

        public NamedUserModel GetView(object user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            UserWithRoles<ApplicationUser> ur = user as UserWithRoles<ApplicationUser>;
            if (ur == null) throw new ArgumentException(nameof(user));

            return new NamedUserModel
            {
                UserName = ur.User.UserName,
                Email = ur.User.Email,
                FirstName = ur.User.FirstName,
                LastName = ur.User.LastName,
                Roles = ur.Roles,
                EmailConfirmed = ur.User.EmailConfirmed,
                LockoutEnabled = ur.User.LockoutEnabled,
                LockoutEnd = ur.User.LockoutEnd?.UtcDateTime
            };
        }

        public Dictionary<string, string> GetMessageDictionary(ApplicationUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            return new Dictionary<string, string>
            {
                ["FirstName"] = user.FirstName,
                ["LastName"] = user.LastName,
                ["UserName"] = user.UserName
            };
        }
    }
}
