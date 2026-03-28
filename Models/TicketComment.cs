using System;
using System.Collections.Generic;
using System.Text;

namespace TicketSystem.Models
{
    public class TicketComment
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public int UserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime Vytvoreno { get; set; }
        public string AutorJmeno { get; set; } = string.Empty;
    }
}
