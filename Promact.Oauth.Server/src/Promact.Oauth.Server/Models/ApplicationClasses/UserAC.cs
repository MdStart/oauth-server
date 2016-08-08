﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Promact.Oauth.Server.Models.ApplicationClasses
{
    public class UserAc
    {
        public string Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(255)]
        public string LastName { get; set; }

        public bool IsActive { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(255)]
        public string Password { get; set; }

        public string UserName { get; set; }
    }
}
