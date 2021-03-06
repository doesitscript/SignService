﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SignService.Models
{
    public class CreateSignServiceUserModel
    {
        
        [Required(AllowEmptyStrings = false, ErrorMessage = "Display Name is required")]
        public string DisplayName { get; set; }
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required")]
        public string Username { get; set; }
        public bool Configured { get; set; }
        public string KeyVaultUrl { get; set; }
        public string CertificateName { get; set; }
        public string TimestampUrl { get; set; }
    }
}
