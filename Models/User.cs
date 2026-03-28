using System;
using System.Collections.Generic;
using System.Text;

namespace TicketSystem.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Jmeno { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public string Login { get; set; } = string.Empty;

        public override string ToString() => $"{Jmeno} ({Role})";
    }
}
