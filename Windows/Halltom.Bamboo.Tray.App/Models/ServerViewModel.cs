﻿namespace Halltom.Bamboo.Tray.App.Models
{
    using System;

    public class ServerViewModel
    {
        public Uri ServerAddress { get; set; }
        
        public string FriendlyName { get; set; }
        
        public string Username { get; set; }
        
        public string Password { get; set; }
    }
}