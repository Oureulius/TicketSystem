using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TicketSystem.Models;

namespace TicketSystem.Data
{
    public class TicketCommentRepository
    {
        private readonly string _connStr = DatabaseHelper.ConnectionString;

        public List<TicketComment> GetByTicketId(int ticketId)
        {
            var list = new List<TicketComment>();
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var cmd = new SQLiteCommand(@"
                SELECT c.Id, c.TicketId, c.UserId, c.Text, c.Vytvoreno, u.Jmeno AS AutorJmeno
                FROM TicketComments c
                JOIN Users u ON u.Id = c.UserId
                WHERE c.TicketId = @ticketId
                ORDER BY datetime(c.Vytvoreno) ASC;", conn);

            cmd.Parameters.AddWithValue("@ticketId", ticketId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new TicketComment
                {
                    Id = Convert.ToInt32(r["Id"]),
                    TicketId = Convert.ToInt32(r["TicketId"]),
                    UserId = Convert.ToInt32(r["UserId"]),
                    Text = r["Text"]?.ToString() ?? "",
                    Vytvoreno = DateTime.TryParse(r["Vytvoreno"]?.ToString(), out var dt) ? dt : DateTime.Now,
                    AutorJmeno = r["AutorJmeno"]?.ToString() ?? "Neznámý"
                });
            }

            return list;
        }

        public void Add(int ticketId, int userId, string text)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var cmd = new SQLiteCommand(@"
                INSERT INTO TicketComments (TicketId, UserId, Text)
                VALUES (@ticketId, @userId, @text);", conn);

            cmd.Parameters.AddWithValue("@ticketId", ticketId);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@text", text);
            cmd.ExecuteNonQuery();
        }
    }
}