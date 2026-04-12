namespace TicketSystem.Models
{
    public class CreatorFilterOption
    {
        public int? UserId { get; set; }
        public string Label { get; set; } = string.Empty;

        public override string ToString() => Label;
    }
}