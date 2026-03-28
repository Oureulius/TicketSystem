using System;

namespace TicketSystem.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public string Nadpis { get; set; } = string.Empty;
        public string Popisek { get; set; } = string.Empty;
        public string Status { get; set; } = "Otevřený";
        public string Priorita { get; set; } = "Střední";
        public string Kategorie { get; set; } = string.Empty;
        public DateTime Vytvoreno { get; set; }
        public DateTime? ZmenaCasu { get; set; }
        public int VytvorenoUzivatelem { get; set; } = 1;
        public int? PridelenoUzivatelem { get; set; }
    }
}